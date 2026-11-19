#!/usr/bin/env python3
"""Reproducible 360-day batch-production pressure model for the design's four-stove plan."""
from __future__ import annotations

import heapq
import xml.etree.ElementTree as ET
import math
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HORIZON = 360 * 24.0
STOVE_PROGRESS_PER_DAY = 50.0 / 9.0
UPTIME = 0.8
LINE_RATE = STOVE_PROGRESS_PER_DAY * UPTIME
REFINED_PROGRESS_PER_UNIT = 1.999994 / 2.0  # Measured in the actual 60-tick furnace path.

# Pill demand, initial/target inventory, one-preparation batch yield, and
# furnace progress. Rates mirror the accepted high-frequency design scenario.
PILLS = {
    "Recovery": (20.0, 48, 12, 2.0),
    "Gather": (4.0, 12, 12, 2.0),
    "Combat": (60.0 / 7.0, 60, 12, 4.0),
    "Dispel": (10.0, 72, 12, 4.0),
    "Purify": (0.1, 3, 3, 20.0),
    "Capacity": (0.1, 3, 3, 25.0),
    "Rescue": (0.3, 9, 3, 10.0),
}
PREPARATIONS = {
    "Recovery": "MX_RecoveryPreparation",
    "Gather": "MX_GatherPreparation",
    "Combat": "MX_CombatPreparation",
    "Dispel": "MX_DispelPreparation",
    "Purify": "MX_PurifyPreparation",
    "Capacity": "MX_CapacityPreparation",
    "Rescue": "MX_RescuePreparation",
}
TARGETS = {name: spec[1] for name, spec in PILLS.items()}
TARGETS.update(Qi=5)
DEMAND_GROUP = {"Recovery": "B", "Gather": "B"}
DEMAND_GROUP.update({name: "CD" for name in PILLS if name not in ("Recovery", "Gather")})


def recipe_inputs() -> dict[str, Counter[str]]:
    result: dict[str, Counter[str]] = {}
    for path in (ROOT / "1.6/Defs/RecipeDefs/Medicines.xml",):
        defs = ET.parse(path).getroot()
        for recipe in defs.findall("RecipeDef"):
            output = recipe.find("products")
            if output is None:
                continue
            products = list(output)
            if len(products) != 1:
                continue
            resources: Counter[str] = Counter()
            ingredients = recipe.find("ingredients")
            if ingredients is not None:
                for ingredient in ingredients.findall("li"):
                    amount = float(ingredient.findtext("count", "0"))
                    defs_node = ingredient.find("filter/thingDefs")
                    names = [] if defs_node is None else [node.text for node in defs_node.findall("li")]
                    if len(names) == 1:
                        resources[names[0]] += amount
                    elif names:
                        resources["choice:" + "|".join(names)] += amount
            product = products[0]
            result[product.tag] = resources
    return result


def scenario_demands():
    # Stagger each of five pawns' 14-hour Combat dose; this avoids an
    # unrealistic same-tick burst while preserving the exact daily rate.
    streams = []
    for name, (per_day, *_rest) in PILLS.items():
        period = 24.0 / per_day
        if name == "Combat":
            individual_period = 14.0
            for pawn in range(5):
                streams.append((name, 2.8 * (pawn + 1), individual_period))
        elif name in ("Purify", "Capacity"):
            due = period if name == "Purify" else 120.0
            streams.append((name, due, period))
        elif name == "Rescue":
            period *= 3
            for dose in range(3):
                streams.append((name, 80.0 * (dose + 1), period))
        else:
            streams.append((name, period, period))
    # Five colonists progress one level every 56.25 days in aggregate.
    streams.append(("Qi", 270.0, 270.0))
    return streams


def run():
    stock = Counter(TARGETS)
    used = Counter()
    produced = Counter()
    batches = Counter()
    started_batches = Counter()
    fuel_units = 0
    stockouts = Counter()
    minimum = Counter(stock)
    prep_resources = recipe_inputs()
    resource_totals = Counter()

    demand_heap = []
    streams = scenario_demands()
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
        for name, (per_day, _target, yield_count, progress) in PILLS.items():
            if DEMAND_GROUP[name] != group:
                continue
            projected = stock[name] + active_by_name[name] * yield_count
            if projected < TARGETS[name]:
                cover = stock[name] / per_day if per_day else float("inf")
                values.append((cover, name, yield_count, progress))
        return values

    def dispatch():
        nonlocal fuel_units
        # Dedicated Qi line: launch immediately and repeat on completion.
        if free_times["A"] <= now and not any(line == "A" for _, line, _ in active):
            duration = 50.0 / (LINE_RATE * REFINED_PROGRESS_PER_UNIT) * 24.0
            finish = now + duration
            active.append((finish, "A", "Qi"))
            heapq.heappush(event_heap, (finish, "production", "A", "Qi"))
            free_times["A"] = finish
            line_progress["A"] += 50.0
            started_batches["Qi"] += 1
            fuel_units += math.ceil(50.0 * 15.0 + 0.01)

        for line in ("B", "C", "D"):
            if free_times[line] > now:
                continue
            group = "B" if line == "B" else "CD"
            options = candidates(group)
            if not options:
                continue
            _cover, name, yield_count, progress = min(options)
            duration = progress / (LINE_RATE * REFINED_PROGRESS_PER_UNIT) * 24.0
            finish = now + duration
            active.append((finish, line, name))
            active_by_name[name] += 1
            line_progress[line] += progress
            started_batches[name] += 1
            # The vanilla stove accumulates float progress every 60 ticks.
            # A tiny shortfall at the threshold can require one more fuel unit.
            fuel_units += math.ceil(progress * 15.0 + 0.01)
            for material, amount in prep_resources.get(PREPARATIONS[name], {}).items():
                resource_totals[material] += amount
            heapq.heappush(event_heap, (finish, "production", line, (name, yield_count)))
            free_times[line] = finish

    def update_minimum():
        for name in TARGETS:
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
                stock["Qi"] += 1
                produced["Qi"] += 1
                batches["Qi"] += 1
            else:
                name, yield_count = payload
                active_by_name[name] -= 1
                stock[name] += yield_count
                produced[name] += yield_count
                batches[name] += 1

        while demand_heap and abs(demand_heap[0][0] - now) < 1e-6:
            _time, stream_id, name, period = heapq.heappop(demand_heap)
            if stock[name] > 0:
                stock[name] -= 1
                used[name] += 1
                update_minimum()
            else:
                stockouts[name] += 1
            nxt = now + period
            if nxt <= HORIZON + 1e-8:
                heapq.heappush(demand_heap, (nxt, stream_id, name, period))
        if now < HORIZON:
            dispatch()

    # Direct-work medicines and the upstream Jade Essence Powder route.
    for name, daily, recipe, yield_count in (
        ("MX_WarmingPill", 2.5, "MX_MakeWarmingPill", 5),
        ("MX_CoolingPill", 2.5, "MX_MakeCoolingPills", 5),
        ("MX_IndustryPill", 5.0, "MX_MakeIndustryPills", 5),
    ):
        batches_per_year = daily * 360.0 / yield_count
        for material, amount in prep_resources.get(name, {}).items():
            resource_totals[material] += amount * batches_per_year

    # Runtime Defs: one LotlqiPellet takes 10 Axolotl_ingredient; the
    # MakeAxolotlIngredientx5 recipe makes 5 from 60 leaves, 2.5 Gold,
    # 10 Uranium, and 10 Jade Essence Powder. Two batches make one pill.
    for material, per_pill in {
        "PsychoidLeaves": 120,
        "Gold": 5,
        "Uranium": 20,
        "Axolotl_JadeEssencePowder": 20,
    }.items():
        resource_totals[material] += per_pill * started_batches["Qi"]

    total_progress = sum(progress * batches[name] for name, (*_head, progress) in PILLS.items()) + 50 * batches["Qi"]
    expected_progress = 17.1349 * 360
    print("Scenario: 5 living MoeLotl + 1 zombie, high-frequency pressure, 4 industrial stoves, 4 pillars each, 80% uptime")
    print(f"Horizon: 360 game days; design demand progress={expected_progress:.1f}; completed-batch progress={total_progress:.1f}; started-batch progress={sum(line_progress.values()):.1f}")
    print("Pill            used   produced  batches  min-stock  end-stock  stockouts   WIP")
    for name in ("Qi", *PILLS.keys()):
        wip = started_batches[name] - batches[name]
        print(f"{name:14} {used[name]:6} {produced[name]:9} {batches[name]:8} {minimum[name]:10} {stock[name]:10} {stockouts[name]:10} {wip:5}")
    print("\nBatch resources consumed (preparation recipes, generated from current XML):")
    for material, amount in sorted(resource_totals.items()):
        print(f"{material:30} {amount:10.1f} total  {amount / 360.0:8.3f}/day")
    print(f"\nRefined batch fuel incl. whole-unit headroom: {fuel_units} Chemfuel / {fuel_units / 360.0:.2f} per day")
    print("\nLimitations: this is a deterministic batch/stock model, not a simulation of pawns, hauling, maps, crop yields, trading, random outages, or furnace fuel delivery.")
    if stockouts:
        raise SystemExit("FAIL: stockouts occurred: " + ", ".join(f"{k}={v}" for k, v in sorted(stockouts.items())))


if __name__ == "__main__":
    run()
