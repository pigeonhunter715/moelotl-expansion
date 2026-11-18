"""Validate the implemented milestone without loading a player save.
Optional --rimsearcher checks external references against the merged Def snapshot.
"""
import argparse
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXPECTED = {
    "Warming": {"label": "暖脉丹", "ticks": 120000, "work": 1200, "skill": 2, "price": 12,
                "ingredients": {"RM_AxoPeachWood": 5, "MedicineHerbal": 2},
                "offsets": {"ComfyTemperatureMin": -15}, "factors": {}},
    "Cooling": {"label": "清暑丹", "ticks": 120000, "work": 1200, "skill": 2, "price": 12,
                "ingredients": {"RM_OvercastWood": 5, "MedicineHerbal": 2},
                "offsets": {"ComfyTemperatureMax": 15}, "factors": {}},
    "Industry": {"label": "百工丹", "ticks": 60000, "work": 6000, "skill": 4, "price": 25,
                 "ingredients": {"PsychoidLeaves": 6, "MedicineHerbal": 2, "Axolotl_JadeEssencePowder": 10},
                 "offsets": {}, "factors": {"WorkSpeedGlobal": 1.2, "ResearchSpeed": 1.1}},
}


def check(condition, message):
    if not condition:
        raise ValueError(message)


def indexed(path, tag):
    nodes = ET.parse(ROOT / path).getroot().findall(tag)
    result = {}
    for node in nodes:
        name = node.findtext("defName")
        check(name and name not in result, f"Duplicate or empty defName: {path}")
        result[name] = node
    return result


def modifiers(node, field):
    return {x.tag: float(x.text) for x in node.findall(f"stages/li/{field}/*")}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rimsearcher", type=Path)
    args = parser.parse_args()
    for folder in ("About", "1.6/Defs", "Languages"):
        for path in (ROOT / folder).rglob("*.xml"):
            ET.parse(path)
    ET.parse(ROOT / "LoadFolders.xml")
    ET.parse(ROOT / "Source/MoeLotlExpansion.csproj")
    meta = ET.parse(ROOT / "About/About.xml").getroot()
    check(meta.findtext("packageId") == "pigeonhunter715.moelotl.expansion", "Package ID changed")
    check([n.text for n in meta.findall("supportedVersions/li")] == ["1.6"], "Unverified game version advertised")
    check({n.text for n in meta.findall("modDependencies/li/packageId")} ==
          {"hentailoliteam.axolotl", "fxz.moelotlzombie.update"}, "Dependency declaration mismatch")
    items = indexed("1.6/Defs/ThingDefs/Medicines.xml", "ThingDef")
    effects = indexed("1.6/Defs/HediffDefs/MedicineEffects.xml", "HediffDef")
    recipes = indexed("1.6/Defs/RecipeDefs/Medicines.xml", "RecipeDef")
    check(len(items) == len(effects) == len(recipes) == 3, "Milestone must have exactly three working medicines")
    external = {("ThingDef", "Axolotl_GrindingTable"), ("ResearchProjectDef", "Axolotl_Research_BasePlant"),
                ("ThingCategoryDef", "Axolotl_Speical"), ("JobDef", "UseItem"), ("SkillDef", "Crafting"),
                ("StatDef", "GeneralLaborSpeed"), ("EffecterDef", "Tailor"), ("SoundDef", "Recipe_MakeStoneBlocks")}
    design = (ROOT / "萌螈丹药扩展设计_v0.1.md").read_text(encoding="utf-8-sig")
    material_names = {"RM_AxoPeachWood": "桃木", "RM_OvercastWood": "阴沉木", "MedicineHerbal": "草药",
                      "PsychoidLeaves": "精神叶", "Axolotl_JadeEssencePowder": "玉琼粉"}
    for key, spec in EXPECTED.items():
        item = items[f"MX_{key}Pill"]
        effect = effects[f"MX_{key}Effect"]
        recipe = recipes[f"MX_Make{key}Pills"]
        check(item.findtext("alwaysHaulable") == "true", f"{key}: item cannot be hauled")
        check(item.findtext("stackLimit") == "25", f"{key}: stack size")
        check(float(item.findtext("statBases/Mass")) == 0.05, f"{key}: mass")
        check(float(item.findtext("statBases/MarketValue")) == spec["price"], f"{key}: price")
        check(item.find("ingestible") is None, f"{key}: accidental food/drug ingestion route")
        comps = item.findall("comps/li")
        check(not any("DestroySelf" in (c.get("Class", "") + (c.findtext("compClass") or "")) for c in comps), f"{key}: double consumption")
        use = next(c for c in comps if c.get("Class") == "CompProperties_Usable")
        check(use.findtext("compClass") == "MoeLotlExpansion.CompUsableMedicine", f"{key}: custom validation missing")
        check(use.findtext("useDuration") == "100", f"{key}: use duration")
        extension = item.find("modExtensions/li")
        check(extension.findtext("effect") == f"MX_{key}Effect", f"{key}: effect cross reference")
        check(int(extension.findtext("durationTicks")) == spec["ticks"], f"{key}: duration")
        check(effect.findtext("hediffClass") == "MoeLotlExpansion.Hediff_TimedMedicine", f"{key}: persistence class")
        check(modifiers(effect, "statOffsets") == spec["offsets"], f"{key}: stat offsets")
        check(modifiers(effect, "statFactors") == spec["factors"], f"{key}: stat factors")
        ingredients = {n.findtext("filter/thingDefs/li"): int(n.findtext("count")) for n in recipe.findall("ingredients/li")}
        check(ingredients == spec["ingredients"], f"{key}: recipe ingredients")
        check({n.text for n in recipe.findall("fixedIngredientFilter/thingDefs/li")} == set(ingredients), f"{key}: ingredient filter")
        check(recipe.findtext(f"products/MX_{key}Pill") == "5", f"{key}: batch output")
        check(int(recipe.findtext("workAmount")) == spec["work"], f"{key}: work")
        check(recipe.findtext("workSkill") == "Crafting", f"{key}: must use crafting skill")
        check(int(recipe.findtext("skillRequirements/Crafting")) == spec["skill"], f"{key}: skill requirement")
        check([n.text for n in recipe.findall("recipeUsers/li")] == ["Axolotl_GrindingTable"], f"{key}: wrong workbench")
        check(recipe.findtext("researchPrerequisite") == "Axolotl_Research_BasePlant", f"{key}: research")
        matching = [line for line in design.splitlines() if line.startswith(f'| {spec["label"]} |') and "颗" in line and len(line.split("|")) == 8]
        check(len(matching) == 1, f"{key}: ambiguous current design recipe")
        cells = [x.strip() for x in matching[0].split("|")]
        expected_materials = {f"{material_names[name]}{n}" for name, n in ingredients.items()}
        check(set(cells[2].split("、")) == expected_materials and cells[3] == "5颗" and int(cells[4]) == spec["work"], f"{key}: implementation differs from accepted design")
        external |= {("ThingDef", name) for name in ingredients}
        external |= {("StatDef", name) for name in (*spec["offsets"], *spec["factors"])}
    check(abs(1.2 * 1.1 - 1.32) < 1e-9, "Research multiplier")
    cn = ET.parse(ROOT / "Languages/ChineseSimplified/Keyed/Medicines.xml").getroot()
    en = ET.parse(ROOT / "Languages/English/Keyed/Medicines.xml").getroot()
    check({x.tag for x in cn} == {x.tag for x in en}, "Translation keys mismatch")
    if args.rimsearcher:
        for def_type, name in sorted(external):
            result = subprocess.run([str(args.rimsearcher.resolve()), "get", name, "--type", def_type, "--brief"],
                                    capture_output=True, text=True, encoding="utf-8", check=True)
            info = json.loads(result.stdout)
            check(info.get("def_name") == name, f"Missing runtime reference: {name}")
        print(f"PASS: {len(external)} external Def references resolved against runtime snapshot")
    print("PASS: 3 medicines, effects, recipes, design parity, XML and localization checks")
    print("Not an in-game load or save/reload test.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
