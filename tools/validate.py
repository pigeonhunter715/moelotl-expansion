"""Validate the implemented milestone without loading a player save.
Optional --rimsearcher checks external references against the merged Def snapshot.
"""
import argparse
import json
import re
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
    "Recovery": {"label": "回气丹", "ticks": 15000, "work": 7200, "skill": 4, "price": 60, "refine": 2,
                 "ingredients": {"PsychoidLeaves": 12, "Axolotl_JadeEssencePowder": 12, "MedicineHerbal": 2, "Uranium": 1},
                 "offsets": {}, "factors": {}},
    "Combat": {"label": "临战丹", "ticks": 35000, "work": 9600, "skill": 6, "price": 100, "refine": 4,
               "ingredients": {"RM_AxoPeachWood": 8, "PsychoidLeaves": 12, "Axolotl_JadeEssencePowder": 16, "Uranium": 2},
               "offsets": {}, "factors": {"MeleeDamageFactor": 1.2, "MoveSpeed": 1.15, "AimingDelayFactor": 0.85, "IncomingDamageFactor": 0.9, "WorkSpeedGlobal": 0.9}},
}


ZOMBIE_SPECS = {
    'Capacity': ('纳煞丹', 600000, 3000, 8, 200, 25, 3, 'Axolotl_Research_Alchemy_Industrial', {'RM_OvercastWood': 15, 'RM_UnrottenBone': 6, 'Axolotl_JadeEssencePowder': 20}),
    'Purify': ('涤煞丹', 600000, 2400, 6, 140, 20, 3, 'Axolotl_Research_Alchemy', {'RM_AxoPeachWood': 15, 'RM_ChickenBlood': 1, 'Gold': 2, 'Axolotl_JadeEssencePowder': 10}),
    'Gather': ('聚煞丹', 15000, 7200, 5, 70, 2, 12, 'Axolotl_Research_Alchemy', {'RM_OvercastWood': 8, 'RM_InsectBlood': 2, 'Axolotl_JadeEssencePowder': 8}),
    'Release': ('归真丹', 0, 2400, 4, 40, 0, 3, 'Axolotl_Research_BasePlant', {'RM_AxoPeachWood': 5, 'RM_OvercastWood': 5, 'MedicineHerbal': 3, 'Axolotl_JadeEssencePowder': 20}),
    'Rescue': ('安魂丹', 0, 3000, 6, 180, 10, 3, 'Axolotl_Research_Alchemy', {'RM_AxoPeachWood': 20, 'RM_ChickenBlood': 3, 'Gold': 6, 'Axolotl_JadeEssencePowder': 20, 'MedicineHerbal': 4}),
}
EXPECTED['Dispel'] = dict(label='镇煞丹', ticks=30000, work=9600, skill=6, price=100, refine=4,
    ingredients={'RM_AxoPeachWood':8, 'RM_ChickenBlood':2, 'Gold':1, 'Axolotl_JadeEssencePowder':12}, offsets={}, factors={})
for key, (label, ticks, work, skill, price, refine, batch, research, ingredients) in ZOMBIE_SPECS.items():
    EXPECTED[key] = dict(label=label, ticks=ticks, work=work, skill=skill, price=price, batch=batch, research=research, ingredients=ingredients, offsets={}, factors={}, zombie=True)
    if refine: EXPECTED[key]['refine'] = refine


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
          {"hentailoliteam.axolotl", "fxz.moelotlzombie.update", "brrainz.harmony"}, "Dependency declaration mismatch")
    items = indexed("1.6/Defs/ThingDefs/Medicines.xml", "ThingDef")
    effects = indexed("1.6/Defs/HediffDefs/MedicineEffects.xml", "HediffDef")
    recipes = indexed("1.6/Defs/RecipeDefs/Medicines.xml", "RecipeDef")
    preparations = indexed("1.6/Defs/ThingDefs/Preparations.xml", "ThingDef")
    check(len(items) == len(recipes) == len(EXPECTED) and len(effects) == sum(s['ticks'] > 0 for s in EXPECTED.values()), "Medicine count mismatch")
    check(len(preparations) == sum("refine" in s for s in EXPECTED.values()), "Preparation count mismatch")
    external = {("ThingDef", "Axolotl_GrindingTable"), ("ResearchProjectDef", "Axolotl_Research_BasePlant"),
                ("ThingCategoryDef", "Axolotl_Speical"), ("JobDef", "UseItem"), ("SkillDef", "Crafting"),
                ("StatDef", "GeneralLaborSpeed"), ("EffecterDef", "Tailor"), ("SoundDef", "Recipe_MakeStoneBlocks")}
    design = (ROOT / "萌螈丹药扩展设计_v0.1.md").read_text(encoding="utf-8-sig")
    material_names = {"RM_AxoPeachWood": "桃木", "RM_OvercastWood": "阴沉木", "MedicineHerbal": "草药",
                      "PsychoidLeaves": "精神叶", "Axolotl_JadeEssencePowder": "玉琼粉", "Uranium": "铀",
                      "RM_UnrottenBone": "不化骨", "RM_ChickenBlood": "鸡血", "RM_InsectBlood": "虫血", "Gold": "黄金"}
    for key, spec in EXPECTED.items():
        item = items[f"MX_{key}Pill"]
        effect = effects.get(f"MX_{key}Effect")
        zombie = spec.get('zombie', False)
        refined = "refine" in spec
        recipe = recipes[f"MX_Make{key}Preparation" if refined else f"MX_Make{key}Pills"]
        research = spec.get('research', "Axolotl_Research_Alchemy" if refined else "Axolotl_Research_BasePlant")
        researches = [research] + (['Axolotl_RM_Zombies'] if zombie or key == 'Dispel' else [])
        batch = spec.get('batch', 12 if refined else 5)
        check(item.findtext("alwaysHaulable") == "true", f"{key}: item cannot be hauled")
        check(item.findtext("stackLimit") == "25", f"{key}: stack size")
        check(float(item.findtext("statBases/Mass")) == 0.05, f"{key}: mass")
        check(float(item.findtext("statBases/MarketValue")) == spec["price"], f"{key}: price")
        check(item.find("ingestible") is None, f"{key}: accidental food/drug ingestion route")
        comps = item.findall("comps/li")
        check(not any("DestroySelf" in (c.get("Class", "") + (c.findtext("compClass") or "")) for c in comps), f"{key}: double consumption")
        use = next(c for c in comps if c.get("Class") == "CompProperties_Usable")
        check(use.findtext("compClass") == ('MoeLotlExpansion.CompFeedMedicine' if zombie else "MoeLotlExpansion.CompUsableMedicine"), f"{key}: custom validation missing")
        check(use.findtext("useDuration") == ('120' if zombie else "100"), f"{key}: use duration")
        extension = item.find("modExtensions/li")
        if zombie:
            check(extension.findtext('zombieMedicine') == key, f'{key}: action')
            check(use.findtext('useJob') == 'MX_FeedMedicine', f'{key}: feeding job')
            check(any(c.findtext('compClass') == 'MoeLotlExpansion.CompTargetableMedicine' for c in comps), f'{key}: targeting')
        if spec['ticks']:
            check(extension.findtext('effect') == f'MX_{key}Effect', f'{key}: effect reference')
            check(int(extension.findtext('durationTicks')) == spec['ticks'], f'{key}: duration')
            effect_class = {'Combat': 'Hediff_CombatMedicine', 'Capacity': 'Hediff_CapacityMedicine', 'Purify': 'Hediff_PurifyMedicine'}.get(key, 'Hediff_TimedMedicine')
            check(effect.findtext('hediffClass') == 'MoeLotlExpansion.' + effect_class, f'{key}: persistence class')
            check(modifiers(effect, 'statOffsets') == spec['offsets'], f'{key}: offsets')
            check(modifiers(effect, 'statFactors') == spec['factors'], f'{key}: factors')
        else:
            check(effect is None and extension.find('effect') is None, f'{key}: instant action')
        ingredients = {n.findtext("filter/thingDefs/li"): int(n.findtext("count")) for n in recipe.findall("ingredients/li")}
        check(ingredients == spec["ingredients"], f"{key}: recipe ingredients")
        check({n.text for n in recipe.findall("fixedIngredientFilter/thingDefs/li")} == set(ingredients), f"{key}: ingredient filter")
        if refined:
            check(recipe.findtext(f"products/MX_{key}Preparation") == "1", f"{key}: prepared batch output")
            prep = preparations[f"MX_{key}Preparation"]
            check(prep.find("ingestible") is None and prep.find("modExtensions") is None, f"{key}: preparation cannot be consumed")
            stove = next(c for c in comps if c.get("Class") == "Axolotl.CompProperties_PillStuffNeed")
            check(stove.findtext("StuffDef") == f"MX_{key}Preparation" and stove.findtext("StuffCount") == "1", f"{key}: no duplicate ingredient charges")
            check(int(stove.findtext("FinishGetCount")) == batch and int(stove.findtext("RefineNeedCount")) == spec["refine"], f"{key}: furnace yield/timing")
            unlock = item.find("modExtensions/li[@Class='MoeLotlExpansion.AlchemyRecipeExtension']")
            check([n.text for n in unlock.findall("researches/li")] == researches, f"{key}: furnace research")
            if not zombie:
                check(extension.findtext("requiresMeridians") == "true" and (key == 'Dispel' or extension.findtext("preventRefresh") == "true"), f"{key}: use gates")
        else:
            check(recipe.findtext(f"products/MX_{key}Pill") == str(batch), f"{key}: batch output")
        check(int(recipe.findtext("workAmount")) == spec["work"], f"{key}: work")
        check(recipe.findtext("workSkill") == "Crafting", f"{key}: must use crafting skill")
        check(int(recipe.findtext("skillRequirements/Crafting")) == spec["skill"], f"{key}: skill requirement")
        check([n.text for n in recipe.findall("recipeUsers/li")] == ["Axolotl_GrindingTable"], f"{key}: wrong workbench")
        actual_researches = [r.text for r in recipe.findall('researchPrerequisites/li')] if zombie or key == 'Dispel' else [recipe.findtext('researchPrerequisite')]
        check(actual_researches == researches, f'{key}: research')
        external |= {('ResearchProjectDef', r) for r in researches}
        matching = [line for line in design.splitlines() if line.startswith(f'| {spec["label"]} |') and "颗" in line and len(line.split("|")) == 8]
        check(len(matching) == 1, f"{key}: ambiguous current design recipe")
        cells = [x.strip() for x in matching[0].split("|")]
        expected_materials = {f"{material_names[name]}{n}" for name, n in ingredients.items()}
        check(set(cells[2].split("、")) == expected_materials and cells[3] == f"{batch}颗" and int(cells[4]) == spec["work"], f"{key}: implementation differs from accepted design")
        if refined:
            check(int(cells[5]) == spec["refine"], f"{key}: furnace design mismatch")
        external |= {("ThingDef", name) for name in ingredients}
        external |= {("StatDef", name) for name in (*spec["offsets"], *spec["factors"])}
    check(abs(1.2 * 1.1 - 1.32) < 1e-9, "Research multiplier")
    pendant = indexed('1.6/Defs/ThingDefs/MedicinePendant.xml', 'ThingDef')['MX_MedicinePendant']
    pendant_recipe = indexed('1.6/Defs/RecipeDefs/MedicinePendant.xml', 'RecipeDef')['MX_MakeMedicinePendant']
    cost = {'Jade': 20, 'Gold': 2, 'Axolotl_JadeEssencePowder': 8, 'Cloth': 5}
    check({n.tag: int(n.text) for n in pendant.findall('costList/*')} == cost, 'Pendant cost list')
    check({n.findtext('filter/thingDefs/li'): int(n.findtext('count')) for n in pendant_recipe.findall('ingredients/li')} == cost, 'Pendant recipe ingredients')
    check(pendant.findtext('thingClass') == 'MoeLotlExpansion.MedicinePendant', 'Pendant physical container class')
    check(pendant.findtext('statBases/Mass') == '0.3' and pendant.findtext('statBases/MaxHitPoints') == '120', 'Pendant mass and durability')
    check(pendant.findtext('apparel/layers/li') == 'MX_PendantLayer' and pendant.findtext('apparel/bodyPartGroups/li') == 'Waist', 'Pendant layer and body part')
    check(not any('Quality' in n.get('Class', '') for n in pendant.findall('comps/li')), 'Pendant quality must not alter capacity')
    check(pendant_recipe.findtext('workAmount') == '6000' and pendant_recipe.findtext('skillRequirements/Crafting') == '4', 'Pendant crafting work and level')
    check(pendant_recipe.findtext('recipeUsers/li') == 'TableSculpting' and pendant_recipe.findtext('researchPrerequisite') == 'Axolotl_Research_BasePlant', 'Pendant bench and research')
    check(pendant_recipe.findtext('products/MX_MedicinePendant') == '1', 'Pendant output')
    external |= {('ThingDef', name) for name in cost}
    external |= {('ThingDef', 'TableSculpting'), ('ApparelLayerDef', 'OnSkin'), ('BodyPartGroupDef', 'Waist'), ('ThingCategoryDef', 'Apparel'), ('WorkTypeDef', 'Hauling')}
    cn = ET.parse(ROOT / "Languages/ChineseSimplified/Keyed/Medicines.xml").getroot()
    en = ET.parse(ROOT / "Languages/English/Keyed/Medicines.xml").getroot()
    check({x.tag for x in cn} == {x.tag for x in en}, "Translation keys mismatch")
    keys = {x.tag for x in cn}
    source = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (ROOT / 'Source').rglob('*.cs'))
    used_keys = set(re.findall(r'"(MX_[A-Za-z0-9_]+)"\.Translate\(', source))
    check(used_keys <= keys, 'Missing source translations: ' + str(used_keys - keys))
    check(len(keys) == len(cn) == len(en), 'Duplicate localization keys')
    if args.rimsearcher:
        for def_type, name in sorted(external):
            result = subprocess.run([str(args.rimsearcher.resolve()), "get", name, "--type", def_type, "--brief"],
                                    capture_output=True, text=True, encoding="utf-8", check=True)
            info = json.loads(result.stdout)
            check(info.get("def_name") == name, f"Missing runtime reference: {name}")
        print(f"PASS: {len(external)} external Def references resolved against runtime snapshot")
    print(f"PASS: {len(items)} medicines, {len(preparations)} preparations, effects, recipes, design parity, XML and localization checks")
    print("Not an in-game load or save/reload test.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
