#!/usr/bin/env python3
"""Reproducible 360-day batch-production pressure model for the four-furnace plan.

The model reads the real 1.6 Defs at runtime:
  * ThingDefs/Medicines.xml: Axolotl.CompProperties_PillStuffNeed supplies the
    required preparation count, furnace progress (RefineNeedCount), and
    finished-pill yield (FinishGetCount).
  * RecipeDefs/MedicineBatch.xml: one x5 bench bill supplies the five
    preparations consumed by one ordinary furnace batch.

Both profiles use five living MoeLotl plus one zombie, four industrial
furnaces, 80% uptime and a 360-day horizon. Standard demand includes one
five-pawn battle every three days. The continuous profile uses the historical
maximum-frequency battle doses and inventories.
"""
from __future__ import annotations

import argparse
import heapq
import math
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DAYS = 360.0
HORIZON = DAYS * 24.0  # Game hours; all event times in this model use hours.
STOVE_PROGRESS_PER_DAY = 50.0 / 9.0
UPTIME = 0.8
LINE_RATE = STOVE_PROGRESS_PER_DAY * UPTIME
REFINED_PROGRESS_PER_UNIT = 1.999994 / 2.0  # Measured 60-tick furnace path.
WORK_PER_GAME_HOUR = 2500.0  # workAmount is in ticks at 100% work speed.

FIGHTERS = 5
COMBAT_CYCLE_HOURS = 18.0

# High-frequency demand scenario: (pills/day, initial/target stock).
# Recovery uses an assumed draw of 20 pills/day. Furnace parameters come from ThingDefs.
PILLS = {
    "Recovery": (20.0, 48),
    "Gather": (4.0, 12),
    "Combat": (FIGHTERS * 24.0 / COMBAT_CYCLE_HOURS, 60),
    "Dispel": (10.0, 72),
    "Purify": (0.1, 3),
    "Capacity": (0.1, 3),
    "Rescue": (0.3, 9),
}
BATTLE_INTERVAL_DAYS = 3
DEMAND_GROUP = {"Recovery": "B", "Gather": "B"}
DEMAND_GROUP.update({name: "CD" for name in PILLS if name not in ("Recovery", "Gather")})

# Native Axolotl Qi line (runtime Def snapshot): one LotlqiPellet consumes ten
# Axolotl_ingredient and 50 progress. Two MakeAxolotlIngredientx5 bills (8000
# work each) supply those ten preparations. This is the native upkeep baseline.
QI_PROGRESS = 50.0
QI_YIELD = 1
QI_STUFF_COUNT = 10
QI_X5_WORK = 8000.0
QI_X5_PER_PILL = QI_STUFF_COUNT // 5
QI_X5_INGREDIENTS = {
    "PsychoidLeaves": 60.0,
    "Gold": 2.5,
    "Uranium": 10.0,
    "Axolotl_JadeEssencePowder": 10.0,
}
# One growth pill every 270 game hours (11.25 days): 32 pills over 360 days.
QI_DEMAND_PERIOD_HOURS = 270.0

# Direct bench pills at the historical pressure values. Round up once for the
# full 360-day horizon so each bill is charged in whole batches.
DIRECT = (
    ("MX_WarmingPill", 2.5, "MX_MakeWarmingPillsx5"),
    ("MX_CoolingPill", 2.5, "MX_MakeCoolingPillsx5"),
    ("MX_IndustryPill", 5.0, "MX_MakeIndustryPillsx5"),
)

def load_batch_recipes() -> dict[str, dict]:
    path = ROOT / "1.6/Defs/RecipeDefs/MedicineBatch.xml"
    bills = {}
    for recipe in ET.parse(path).getroot().findall("RecipeDef"):
        name = recipe.findtext("defName")
        products = {product.tag: int(product.text) for product in recipe.findall("products/*")}
        resources: Counter[str] = Counter()
        for ingredient in recipe.findall("ingredients/li"):
            names = [node.text for node in ingredient.findall("filter/thingDefs/li")]
            if not names:
                raise SystemExit(f"FAIL: {name}: ingredient has no explicit ThingDef")
            material = names[0] if len(names) == 1 else "choice:" + "|".join(names)
            resources[material] += int(ingredient.findtext("count"))
        bills[name] = {"work": int(recipe.findtext("workAmount")), "products": products, "ingredients": resources}
    return bills


def load_batch_inputs(recipes: dict[str, dict]) -> dict[str, dict]:
    path = ROOT / "1.6/Defs/ThingDefs/Medicines.xml"
    defs = {thing.findtext("defName"): thing for thing in ET.parse(path).getroot().findall("ThingDef")}
    result = {}
    for name in PILLS:
        pill = defs[f"MX_{name}Pill"]
        comp = pill.find("comps/li[@Class='Axolotl.CompProperties_PillStuffNeed']")
        stuff = f"MX_{name}Preparation"
        bill = recipes[f"MX_Make{name}Preparationx5"]
        if comp is None or comp.findtext("StuffDef") != stuff or comp.findtext("StuffCount") != "5":
            raise SystemExit(f"FAIL: {name} furnace batch does not require five {stuff}")
        if bill["products"] != {stuff: 5}:
            raise SystemExit(f"FAIL: {name} x5 bench bill does not supply one full furnace batch")
        result[name] = {
            "progress": int(comp.findtext("RefineNeedCount")),
            "yield": int(comp.findtext("FinishGetCount")),
            "work": bill["work"],
            "ingredients": bill["ingredients"],
        }
    return result


def scenario_demands(demand, continuous_combat):
    streams = []
    for name, (per_day, _target) in demand.items():
        period = 24.0 / per_day
        if not continuous_combat and name in ("Combat", "Dispel"):
            # Each fighter takes one of each battle pill at the same engagement.
            battle_hours = 24.0 * BATTLE_INTERVAL_DAYS
            streams.extend([(name, battle_hours, battle_hours)] * FIGHTERS)
        elif name == "Combat":
            # Stagger the fighters across the effect and fatigue cycle.
            for fighter in range(FIGHTERS):
                first_dose = COMBAT_CYCLE_HOURS * (fighter + 1) / FIGHTERS
                streams.append((name, first_dose, COMBAT_CYCLE_HOURS))
        elif name in ("Purify", "Capacity"):
            due = period if name == "Purify" else 120.0
            streams.append((name, due, period))
        elif name == "Rescue":
            period *= 3
            for dose in range(3):
                streams.append((name, 80.0 * (dose + 1), period))
        else:
            streams.append((name, period, period))
    streams.append(("Qi", QI_DEMAND_PERIOD_HOURS, QI_DEMAND_PERIOD_HOURS))
    return streams


def run(continuous_combat=False):
    recipes = load_batch_recipes()
    inputs = load_batch_inputs(recipes)

    demand = PILLS.copy()
    if not continuous_combat:
        battle_rate = FIGHTERS / BATTLE_INTERVAL_DAYS
        demand.update(Combat=(battle_rate, 10), Dispel=(battle_rate, 10), Purify=(0.1, 2), Capacity=(0.1, 2))
    targets = {name: target for name, (_per_day, target) in demand.items()}
    targets["Qi"] = 5
    stock = Counter(targets)
    used = Counter()
    produced = Counter()
    batches = Counter()
    started_batches = Counter()
    fuel_units = 0
    stockouts = Counter()
    minimum = Counter(stock)
    demand_events = Counter()
    resource_totals = Counter()

    demand_heap = []
    streams = scenario_demands(demand, continuous_combat)
    for stream_id, (name, due, period) in enumerate(streams):
        if due <= HORIZON:
            heapq.heappush(demand_heap, (due, stream_id, name, period))

    # At most one active batch per furnace. A stays on Qi; B handles Recovery
    # and Gather; C/D rotate the five zombie/combat medicines by lowest cover.
    free_times = {"A": 0.0, "B": 0.0, "C": 0.0, "D": 0.0}
    active = []
    active_by_name = Counter()
    event_heap = []
    now = 0.0
    line_progress = Counter()

    def candidates(group):
        values = []
        for name, (per_day, target) in demand.items():
            if DEMAND_GROUP[name] != group:
                continue
            spec = inputs[name]
            projected = stock[name] + active_by_name[name] * spec["yield"]
            if projected < target:
                cover = stock[name] / per_day if per_day else float("inf")
                values.append((cover, name))
        return values

    def dispatch():
        nonlocal fuel_units
        # Dedicated Qi line: launch immediately and repeat on completion.
        if free_times["A"] <= now and not any(line == "A" for _, line, _ in active):
            duration = QI_PROGRESS / (LINE_RATE * REFINED_PROGRESS_PER_UNIT) * 24.0
            finish = now + duration
            active.append((finish, "A", "Qi"))
            heapq.heappush(event_heap, (finish, "production", "A", ("Qi", QI_YIELD)))
            free_times["A"] = finish
            line_progress["A"] += QI_PROGRESS
            started_batches["Qi"] += 1
            fuel_units += math.ceil(QI_PROGRESS * 15.0 + 0.01)
            for material, amount in QI_X5_INGREDIENTS.items():
                resource_totals[material] += QI_X5_PER_PILL * amount

        for line in ("B", "C", "D"):
            if free_times[line] > now:
                continue
            group = "B" if line == "B" else "CD"
            options = candidates(group)
            if not options:
                continue
            _cover, name = min(options)
            spec = inputs[name]
            duration = spec["progress"] / (LINE_RATE * REFINED_PROGRESS_PER_UNIT) * 24.0
            finish = now + duration
            active.append((finish, line, name))
            active_by_name[name] += 1
            line_progress[line] += spec["progress"]
            started_batches[name] += 1
            # One x5 bench bill is one furnace batch: five preparations.
            fuel_units += math.ceil(spec["progress"] * 15.0 + 0.01)
            for material, amount in spec["ingredients"].items():
                resource_totals[material] += amount
            heapq.heappush(event_heap, (finish, "production", line, (name, spec["yield"])))
            free_times[line] = finish

    def update_minimum():
        for name in targets:
            minimum[name] = min(minimum[name], stock[name])

    dispatch()
    while event_heap or demand_heap:
        next_production = event_heap[0][0] if event_heap else float("inf")
        next_demand = demand_heap[0][0] if demand_heap else float("inf")
        event_time = min(next_production, next_demand)
        if event_time > HORIZON + 1e-6:
            break
        now = min(event_time, HORIZON)

        # Complete production before demands at a shared tick, matching the
        # game tick ordering where the finished batch enters stock immediately.
        while event_heap and abs(event_heap[0][0] - now) < 1e-6:
            _time, _kind, line, payload = heapq.heappop(event_heap)
            active = [entry for entry in active if entry[1] != line]
            free_times[line] = now
            if line == "A":
                stock["Qi"] += QI_YIELD
                produced["Qi"] += QI_YIELD
                batches["Qi"] += 1
            else:
                name, yield_count = payload
                active_by_name[name] -= 1
                stock[name] += yield_count
                produced[name] += yield_count
                batches[name] += 1

        while demand_heap and abs(demand_heap[0][0] - now) < 1e-6:
            _time, stream_id, name, period = heapq.heappop(demand_heap)
            demand_events[name] += 1
            if stock[name] > 0:
                stock[name] -= 1
                used[name] += 1
                update_minimum()
            else:
                stockouts[name] += 1
                minimum[name] = 0
            nxt = now + period
            if nxt <= HORIZON + 1e-8:
                heapq.heappush(demand_heap, (nxt, stream_id, name, period))
        if now < HORIZON:
            dispatch()

    # Direct bench work is outside the furnace schedule but shares the bench.
    # Round up once, after aggregating the whole 360-day demand into x5 bills.
    direct_work = 0
    for product, daily, recipe_name in DIRECT:
        bill = recipes[recipe_name]
        batches_needed = math.ceil(daily * DAYS / bill["products"][product])
        direct_work += batches_needed * bill["work"]
        for material, amount in bill["ingredients"].items():
            resource_totals[material] += amount * batches_needed

    # Native Qi preparation work and materials are charged per started pill.
    qi_bills = started_batches["Qi"] * QI_X5_PER_PILL
    qi_work = qi_bills * QI_X5_WORK

    qi_demand_progress = demand_events["Qi"] * QI_PROGRESS / QI_YIELD
    pill_demand_progress = {
        name: demand_events[name] * inputs[name]["progress"] / inputs[name]["yield"]
        for name in PILLS
    }
    demand_progress = qi_demand_progress + sum(pill_demand_progress.values())
    completed_progress = (
        sum(inputs[name]["progress"] * batches[name] for name in PILLS)
        + QI_PROGRESS * batches["Qi"]
    )
    started_progress = sum(line_progress.values())

    physical_capacity = len(free_times) * STOVE_PROGRESS_PER_DAY * DAYS
    planned_capacity = len(free_times) * LINE_RATE * DAYS
    print("Scenario: 5 living MoeLotl + 1 zombie, 4 industrial furnaces, 4 pillars each, 80% uptime")
    profile = "historical continuous combat" if continuous_combat else f"one five-pawn battle every {BATTLE_INTERVAL_DAYS} days"
    print(f"Horizon: {DAYS:.0f} game days; {profile}")
    print(f"Demand progress: {demand_progress:.1f}; completed-batch progress: {completed_progress:.1f}; started-batch progress: {started_progress:.1f}")
    print(f"Four-furnace capacity: physical {physical_capacity:.1f}; 80%-uptime plan {planned_capacity:.1f}")
    print(f"Utilization of physical capacity: demand {demand_progress / physical_capacity:.1%}, started {started_progress / physical_capacity:.1%}")
    print(f"Utilization of 80% plan: demand {demand_progress / planned_capacity:.1%}, started {started_progress / planned_capacity:.1%}")

    print("\nPill            prog/yield      target    used  started   done  produced  min-stock  end-stock  stockouts    WIP")
    for name in ("Qi", *PILLS):
        if name == "Qi":
            progress, yield_count = QI_PROGRESS, QI_YIELD
        else:
            progress, yield_count = inputs[name]["progress"], inputs[name]["yield"]
        pair = f"{progress:g}/{yield_count}"
        print(
            f"{name:14} {pair:>14} {targets[name]:>9} {used[name]:>7} "
            f"{started_batches[name]:>8} {batches[name]:>6} {produced[name]:>9} "
            f"{minimum[name]:>10} {stock[name]:>10} {stockouts[name]:>10} "
            f"{started_batches[name] - batches[name]:>6}")

    ordinary_progress = sum(pill_demand_progress.values())
    print(f"\nDemand uses {ordinary_progress / (3 * LINE_RATE * DAYS):.1%} of the three ordinary furnaces' 80%-uptime budget.")
    for group, lines in (("B", ("B",)), ("CD", ("C", "D"))):
        progress = sum(pill_demand_progress[name] for name, assigned in DEMAND_GROUP.items() if assigned == group)
        capacity = len(lines) * LINE_RATE * DAYS
        print(f"  {'/'.join(lines)} demand: {progress / capacity:.1%} of its budget")

    ordinary_work = sum(started_batches[name] * inputs[name]["work"] for name in PILLS)
    total_work = ordinary_work + direct_work + qi_work
    print("\nBench work per game day at 100% work speed (excludes milling):")
    for label, work in (("Ordinary furnace preparations", ordinary_work), ("Direct-work pills", direct_work),
                        ("Native Qi preparations", qi_work), ("Total", total_work)):
        print(f"{label:30} {work / (WORK_PER_GAME_HOUR * DAYS):8.3f} h/day")

    print("\nBatch resources consumed (one x5 bill per ordinary furnace batch; x5 direct bills; native Qi baseline):")
    for material, amount in sorted(resource_totals.items()):
        print(f"{material:30} {amount:10.1f} total  {amount / DAYS:8.3f}/day")
    print(f"\nRefined batch fuel incl. whole-unit headroom: {fuel_units} Chemfuel / {fuel_units / DAYS:.2f} per day")

    print("\nRecurring demand covers the pills listed in the schedule and the three direct-work medicines.")
    print("Limitations: deterministic batch/stock model; no pawns, hauling, maps, crops, trade, random outages, or fuel delivery. Upstream Jade Essence Powder milling is not modeled; the 80% uptime is an input.")
    if stockouts:
        raise SystemExit("FAIL: stockouts occurred: " + ", ".join(f"{k}={v}" for k, v in sorted(stockouts.items())))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--continuous-combat", action="store_true", help="Use the historical sustained battle-dose scenario.")
    run(parser.parse_args().continuous_combat)
