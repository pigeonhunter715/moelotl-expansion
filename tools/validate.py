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
    "Warming": {"label": "暖脉丹", "ticks": 120000, "work": 1000, "skill": 2, "price": 12, "output": 5,
                "ingredients": {"RM_AxoPeachWood": 4, "MedicineHerbal": 1, "Axolotl_JadeEssencePowder": 2},
                "offsets": {"ComfyTemperatureMin": -15}, "factors": {}},
    "Cooling": {"label": "清暑丹", "ticks": 120000, "work": 1000, "skill": 2, "price": 12, "output": 5,
                "ingredients": {"RM_OvercastWood": 4, "MedicineHerbal": 1, "Axolotl_JadeEssencePowder": 2},
                "offsets": {"ComfyTemperatureMax": 15}, "factors": {}},
    "Industry": {"label": "百工丹", "ticks": 60000, "work": 4800, "skill": 4, "price": 25, "output": 3,
                 "ingredients": {"PsychoidLeaves": 6, "MedicineHerbal": 2, "Axolotl_JadeEssencePowder": 10},
                 "offsets": {}, "factors": {"WorkSpeedGlobal": 1.2, "ResearchSpeed": 1.1, "GeneralLaborSpeed": 1.15, "ConstructionSpeed": 1.15}},
    "Recovery": {"label": "回气丹", "ticks": 0, "work": 2400, "skill": 4, "price": 60, "progress": 6, "output": 50,
                 "ingredients": {"PsychoidLeaves": 16, "Axolotl_JadeEssencePowder": 16, "MedicineHerbal": 2, "Uranium": 2},
                 "offsets": {}, "factors": {}},
    "Combat": {"label": "临战丹", "ticks": 45000, "work": 3600, "skill": 6, "price": 100, "progress": 10, "output": 25,
               "ingredients": {"RM_AxoPeachWood": 12, "PsychoidLeaves": 16, "Axolotl_JadeEssencePowder": 16, "Uranium": 2},
               "offsets": {"PainShockThreshold": 0.15, "MentalBreakThreshold": -0.10, "MeleeHitChance": 4, "ShootingAccuracyPawn": 4},
               "factors": {"MeleeDamageFactor": 1.2, "MoveSpeed": 1.15, "AimingDelayFactor": 0.85, "IncomingDamageFactor": 0.9, "WorkSpeedGlobal": 0.9}},
}


ZOMBIE_SPECS = {
    'Capacity': ('纳煞丹', 600000, 2400, 8, 200, 40, 3, 'Axolotl_Research_Alchemy_Industrial', {'RM_OvercastWood': 6, 'RM_UnrottenBone': 2, 'Axolotl_JadeEssencePowder': 8}),
    'Purify': ('涤煞丹', 600000, 2000, 6, 140, 32, 3, 'Axolotl_Research_Alchemy', {'RM_AxoPeachWood': 6, 'RM_ChickenBlood': 1, 'Gold': 1, 'Axolotl_JadeEssencePowder': 5}),
    'Gather': ('聚煞丹', 15000, 2000, 5, 70, 6, 30, 'Axolotl_Research_Alchemy', {'RM_OvercastWood': 12, 'RM_InsectBlood': 2, 'Axolotl_JadeEssencePowder': 15}),
    'Release': ('归真丹', 0, 2000, 4, 40, 0, 3, 'Axolotl_Research_BasePlant', {'RM_AxoPeachWood': 4, 'RM_OvercastWood': 4, 'MedicineHerbal': 2, 'Axolotl_JadeEssencePowder': 12}),
    'Rescue': ('安魂丹', 0, 2400, 6, 180, 12, 10, 'Axolotl_Research_Alchemy', {'RM_AxoPeachWood': 10, 'RM_ChickenBlood': 2, 'Gold': 2, 'Axolotl_JadeEssencePowder': 10, 'MedicineHerbal': 2}),
}
EXPECTED['Dispel'] = dict(label='镇煞丹', ticks=30000, work=3200, skill=6, price=100, progress=10, output=25,
    ingredients={'RM_AxoPeachWood': 12, 'RM_ChickenBlood': 3, 'Gold': 2, 'Axolotl_JadeEssencePowder': 16}, offsets={}, factors={})
for key, (label, ticks, work, skill, price, progress, output, research, ingredients) in ZOMBIE_SPECS.items():
    EXPECTED[key] = dict(label=label, ticks=ticks, work=work, skill=skill, price=price, output=output, research=research, ingredients=ingredients, offsets={}, factors={}, zombie=True)
    if progress: EXPECTED[key]['progress'] = progress
EXPECTED['Gather']['factors'] = {'RM_MalevolentFactor': 1.5}


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


def ingredient_counts(recipe):
    return {n.findtext("filter/thingDefs/li"): int(n.findtext("count")) for n in recipe.findall("ingredients/li")}


def check_recipe_quantity_text(recipe, product_label):
    product, = recipe.findall('products/*')
    output = f'{product_label} ×{int(product.text)}'
    name = recipe.findtext('defName')
    check(recipe.findtext('label').endswith(output), f'{name}: displayed bill quantity differs from its product')
    check(output in recipe.findtext('description'), f'{name}: displayed description quantity differs from its product')


def research_requirements(node):
    single = node.findtext('researchPrerequisite')
    return ([single] if single else []) + [r.text for r in node.findall('researchPrerequisites/li')]


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
    group_recipes = indexed("1.6/Defs/RecipeDefs/MedicineBatch.xml", "RecipeDef")
    preparations = indexed("1.6/Defs/ThingDefs/Preparations.xml", "ThingDef")
    effect_names = {f'MX_{key}Effect' for key, spec in EXPECTED.items() if spec['ticks']}
    check(len(items) == len(recipes) == len(EXPECTED) and set(effects) == effect_names | {'MX_RecoveryEffect'}, "Medicine count mismatch")
    check(set(group_recipes) == {name + 'x5' for name in recipes}, 'Bench batch recipes must match the eleven original recipes')
    check(len(preparations) == sum("progress" in s for s in EXPECTED.values()), "Preparation count mismatch")
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
        refined = "progress" in spec
        recipe = recipes[f"MX_Make{key}Preparation" if refined else f"MX_Make{key}Pills"]
        research = spec.get('research', "Axolotl_Research_Alchemy" if refined else "Axolotl_Research_BasePlant")
        researches = [research] + (['Axolotl_RM_Zombies'] if zombie or key == 'Dispel' else [])
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
        elif key == 'Recovery':
            check(extension.findtext('restoresQi') == 'true' and extension.find('effect') is None and
                  extension.find('durationTicks') is None and extension.find('preventRefresh') is None, 'Recovery: immediate use without cooldown')
        else:
            check(effect is None and extension.find('effect') is None, f'{key}: instant action')
        ingredients = ingredient_counts(recipe)
        check(ingredients == spec["ingredients"], f"{key}: recipe ingredients")
        check({n.text for n in recipe.findall("fixedIngredientFilter/thingDefs/li")} == set(ingredients), f"{key}: ingredient filter")
        x5 = group_recipes[recipe.findtext('defName') + 'x5']
        check(ingredient_counts(x5) == {name: count * 5 for name, count in ingredients.items()}, f'{key}: x5 ingredients')
        check({child.tag: int(child.text) for child in x5.findall('products/*')} ==
              {child.tag: int(child.text) * 5 for child in recipe.findall('products/*')}, f'{key}: x5 products')
        check(int(x5.findtext('workAmount')) == spec['work'] * 4, f'{key}: x5 work')
        check(research_requirements(x5) == researches and x5.findtext('workSkill') == 'Crafting' and
              x5.findtext('skillRequirements/Crafting') == str(spec['skill']) and
              x5.findtext('recipeUsers/li') == 'Axolotl_GrindingTable' and
              {n.text for n in x5.findall('fixedIngredientFilter/thingDefs/li')} == set(ingredients), f'{key}: x5 gates')
        product = preparations[f'MX_{key}Preparation'] if refined else item
        for bill in (recipe, x5):
            check_recipe_quantity_text(bill, product.findtext('label'))
        if refined:
            check(recipe.findtext(f"products/MX_{key}Preparation") == "1", f"{key}: prepared batch output")
            prep = preparations[f"MX_{key}Preparation"]
            check(prep.find("ingestible") is None and prep.find("modExtensions") is None, f"{key}: preparation cannot be consumed")
            stove = next(c for c in comps if c.get("Class") == "Axolotl.CompProperties_PillStuffNeed")
            check(stove.findtext("StuffDef") == f"MX_{key}Preparation" and stove.findtext("StuffCount") == "5", f"{key}: five preparations per batch")
            check(int(stove.findtext("FinishGetCount")) == spec["output"] and int(stove.findtext("RefineNeedCount")) == spec["progress"], f"{key}: furnace yield/timing")
            furnace_output = f'{item.findtext("label")} ×{spec["output"]}'
            for entry in (recipe, x5, prep):
                check(furnace_output in entry.findtext('description'), f'{key}: displayed furnace quantity')
            unlock = item.find("modExtensions/li[@Class='MoeLotlExpansion.AlchemyRecipeExtension']")
            check([n.text for n in unlock.findall("researches/li")] == researches, f"{key}: furnace research")
            check(research_requirements(item) == researches, f'{key}: finished pills must appear in research')
            if not zombie:
                check(extension.findtext("requiresMeridians") == "true" and (key != 'Combat' or extension.findtext("preventRefresh") == "true"), f"{key}: use gates")
        else:
            check(recipe.findtext(f"products/MX_{key}Pill") == str(spec["output"]), f"{key}: batch output")
        check(int(recipe.findtext("workAmount")) == spec["work"], f"{key}: work")
        check(recipe.findtext("workSkill") == "Crafting", f"{key}: must use crafting skill")
        check(int(recipe.findtext("skillRequirements/Crafting")) == spec["skill"], f"{key}: skill requirement")
        check([n.text for n in recipe.findall("recipeUsers/li")] == ["Axolotl_GrindingTable"], f"{key}: wrong workbench")
        actual_researches = research_requirements(recipe)
        check(actual_researches == researches, f'{key}: research')
        external |= {('ResearchProjectDef', r) for r in researches}
        matching = [line for line in design.splitlines() if line.startswith(f'| {spec["label"]} |') and "颗" in line and len(line.split("|")) == 9]
        check(len(matching) == 1, f"{key}: ambiguous current design recipe")
        cells = [x.strip() for x in matching[0].split("|")]
        expected_materials = {f"{material_names[name]}{n}" for name, n in ingredients.items()}
        check(set(cells[2].split("、")) == expected_materials and int(cells[3]) == spec["work"] and int(cells[7]) == spec["price"], f"{key}: implementation differs from accepted design")
        if refined:
            check(cells[4] == "1份丹剂" and int(cells[5]) == spec["progress"] and cells[6] == f"{spec['output']}颗", f"{key}: furnace design mismatch")
        else:
            check(cells[4] == f"{spec['output']}颗" and cells[5] == "—" and cells[6] == "—", f"{key}: direct design mismatch")
        external |= {("ThingDef", name) for name in ingredients}
        external |= {("StatDef", name) for name in (*spec["offsets"], *spec["factors"])}
    taiyin_ingredients = {'AxoZombieTailE': 3, 'AxoZombieTailD': 30, 'RM_OvercastWood': 300,
                         'RM_InsectBlood': 3000, 'Axolotl_JadeEssencePowder': 3000}
    taiyin_research = {'Axolotl_Research_HeavenlyTopArmor', 'Axolotl_RM_Zombies', 'Axolotl_Research_Alchemy_Industrial'}
    taiyin_items = indexed('1.6/Defs/ThingDefs/Taiyin.xml', 'ThingDef')
    taiyin = taiyin_items['MX_TaiyinPill']
    taiyin_recipe = indexed('1.6/Defs/RecipeDefs/Taiyin.xml', 'RecipeDef')['MX_MakeTaiyinPreparation']
    check_recipe_quantity_text(taiyin_recipe, taiyin_items['MX_TaiyinPreparation'].findtext('label'))
    for entry in (taiyin_recipe, taiyin_items['MX_TaiyinPreparation']):
        check('太阴丹 ×1' in entry.findtext('description'), 'Taiyin: displayed furnace quantity')
    check(ingredient_counts(taiyin_recipe) == taiyin_ingredients, 'Taiyin recipe ingredients')
    check(taiyin_recipe.findtext('products/MX_TaiyinPreparation') == '1' and
          taiyin_recipe.findtext('recipeUsers/li') == 'Axolotl_GrindingTable' and
          taiyin_recipe.findtext('skillRequirements/Crafting') == '10', 'Taiyin single preparation on the grinding table')
    check(set(research_requirements(taiyin)) == set(research_requirements(taiyin_recipe)) == taiyin_research, 'Taiyin research visibility and prerequisites')
    taiyin_alchemy = taiyin.find("modExtensions/li[@Class='MoeLotlExpansion.AlchemyRecipeExtension']")
    check(taiyin_alchemy.findtext('fixedActiveTicks') == '3600000' and taiyin_alchemy.findtext('industrialOnly') == 'true' and
          {row.text for row in taiyin_alchemy.findall('researches/li')} == taiyin_research, 'Taiyin special exact-year industrial batch')
    taiyin_furnace = taiyin.find("comps/li[@Class='Axolotl.CompProperties_PillStuffNeed']")
    check(taiyin_furnace.findtext('StuffCount') == taiyin_furnace.findtext('FinishGetCount') == '1' and
          taiyin_furnace.findtext('StuffDef') == 'MX_TaiyinPreparation', 'Taiyin furnace result is one pill per preparation')
    taiyin_self_users = {row.text for row in taiyin.findall('comps/li/allowedMutants/li')}
    check(taiyin_self_users == {'RM_PurpleZombie', 'RM_WhiteZombie', 'RM_GreenZombie', 'RM_BeastlyZombie', 'RM_FlyingZombie'},
          'Taiyin opens the manual use confirmation for native zombies')
    check(taiyin.find('ingestible') is None and taiyin_items['MX_TaiyinPreparation'].find('ingestible') is None, 'Taiyin requires intentional medicine use')
    description = (ROOT / 'docs/TAIYIN_PILL.md').read_text(encoding='utf-8').split('## 游戏内物品描述（定稿）', 1)[1].strip()
    check(' '.join(taiyin.findtext('description').split()) == ' '.join(description.split()), 'Taiyin approved item text')
    graphic = taiyin.findtext('graphicData/texPath')
    check((ROOT / f'1.6/Textures/{graphic}.png').is_file(), 'Taiyin finished texture')
    taiyin_effects = indexed('1.6/Defs/HediffDefs/Taiyin.xml', 'HediffDef')
    effect = taiyin_effects['MX_TaiyinEffect']
    check(modifiers(effect, 'statFactors') == {'IncomingDamageFactor': .50, 'MeleeDamageFactor': 1.50,
                                               'MeleeCooldownFactor': .50, 'MoveSpeed': 1.35, 'RM_MalevolentFactor': 2.0}, 'Taiyin stat factors')
    check(effect.findtext('stages/li/capMods/li/capacity') == 'Consciousness' and
          float(effect.findtext('stages/li/capMods/li/offset')) == 1.0, 'Taiyin consciousness')
    external |= {('ThingDef', material) for material in taiyin_ingredients}
    external |= {('ResearchProjectDef', research) for research in taiyin_research}
    external |= {('MutantDef', user) for user in taiyin_self_users}
    check(abs(1.2 * 1.1 - 1.32) < 1e-9, "Research multiplier")
    pendant = indexed('1.6/Defs/ThingDefs/MedicinePendant.xml', 'ThingDef')['MX_MedicinePendant']
    pendant_recipe = indexed('1.6/Defs/RecipeDefs/MedicinePendant.xml', 'RecipeDef')['MX_MakeMedicinePendant']
    cost = {'Jade': 20, 'Gold': 2, 'Axolotl_JadeEssencePowder': 8, 'Cloth': 5}
    check({n.tag: int(n.text) for n in pendant.findall('costList/*')} == cost, 'Pendant cost list')
    check({n.findtext('filter/thingDefs/li'): int(n.findtext('count')) for n in pendant_recipe.findall('ingredients/li')} == cost, 'Pendant recipe ingredients')
    check(pendant.findtext('thingClass') == 'MoeLotlExpansion.MedicinePendant', 'Pendant physical container class')
    check(pendant.findtext('statBases/Mass') == '0.3' and pendant.findtext('statBases/MaxHitPoints') == '120', 'Pendant mass and durability')
    check(pendant.findtext('apparel/layers/li') == 'MX_PendantLayer' and pendant.findtext('apparel/bodyPartGroups/li') == 'Waist', 'Pendant layer and body part')
    check(pendant.findtext('techLevel') == 'Medieval', 'Pendant tech level')
    comps = pendant.findall('comps/li')
    check(any((c.findtext('compClass') or '') == 'CompQuality' for c in comps), 'Pendant quality component')
    shield = next((c for c in comps if c.get('Class') == 'Axolotl.CompProperties_Apparel_LotlQiShield'), None)
    check(shield is not None and int(shield.findtext('LotlQiShieldCount')) == 16, 'Pendant native qi shield component')
    check(shield.findtext('LotlQiShieldReSetOffset') in (None, '1'), 'Pendant native recharge offset')
    check('品质' in pendant.findtext('description') and '护盾' in pendant.findtext('description'), 'Pendant quality and shield description')
    check(pendant_recipe.findtext('workAmount') == '6000' and pendant_recipe.findtext('skillRequirements/Crafting') == '4', 'Pendant crafting work and level')
    check(pendant_recipe.findtext('recipeUsers/li') == 'TableSculpting', 'Pendant bench')
    pendant_researches = [n.text for n in pendant_recipe.findall('researchPrerequisites/li')]
    check(pendant_recipe.find('researchPrerequisite') is None and pendant_researches ==
          ['Axolotl_Research_Alchemy', 'Axolotl_Research_LotlQiClothing'], 'Pendant research prerequisites')
    check(all(name in pendant_recipe.findtext('description') for name in ('气运丹田', '巧织灵裳')), 'Pendant research description')
    check(pendant_recipe.findtext('products/MX_MedicinePendant') == '1', 'Pendant output')
    external |= {('ThingDef', name) for name in cost}
    external |= {('ResearchProjectDef', name) for name in pendant_researches}
    external |= {('ThingDef', 'TableSculpting'), ('ApparelLayerDef', 'OnSkin'), ('BodyPartGroupDef', 'Waist'), ('ThingCategoryDef', 'Apparel'), ('WorkTypeDef', 'Hauling')}
    cn = [node.tag for file in (ROOT / 'Languages/ChineseSimplified/Keyed').glob('*.xml') for node in ET.parse(file).getroot()]
    en = [node.tag for file in (ROOT / 'Languages/English/Keyed').glob('*.xml') for node in ET.parse(file).getroot()]
    keys = set(cn)
    check(keys == set(en), 'Translation keys mismatch')
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
    print(f"PASS: {len(items) + 1} medicines, {len(preparations) + 1} preparations, {len(group_recipes)} x5 bills, effects, research visibility, design parity, XML and localization checks")
    print("Not an in-game load or save/reload test.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
