# Metrics and evidence

All values use logged events, not ship fitting statistics. Logs are read locally without modifying them.

| Metric | Definition and limits |
| --- | --- |
| Encounter duration | First through last combat second, inclusive. Boundaries still inferred by system jumps or a greater-than-five-minute combat gap. Does not include travel or prove site completion. |
| Active combat time | Union of ten-second intervals following recognized incoming/outgoing damage or misses, clipped to encounter bounds. This allowance is a heuristic, not measured weapon uptime. |
| Average outgoing/incoming DPS | Damage divided by estimated active time. Full-encounter DPS is also shown for comparison. |
| Peak incoming DPS | Maximum rolling 1s, 5s, and 10s damage sums divided by the full window size. Windows exclude events exactly one window apart. Includes simultaneous hits. |
| Longest damage gap | Longest interval without successful outgoing damage, including silence before first or after last damage. Timestamps have one-second precision. |
| Damage downtime | Sum of each no-damage interval's portion beyond ten seconds. Includes misses and may include normal long weapon cycles; it is not proof of poor piloting. |
| Weapons and targets | Logged damage totals and shares by exact logged weapon/opponent name. Missing weapon names remain unspecified. Ammo appears only when the logged name identifies it. |
| Incoming sources | Damage totals, shares, and peak five-second DPS by opponent name. |
| Hit quality | Counts and percentages split incoming/outgoing, including recognized misses. Original quality labels are preserved. Non-damage combat messages such as scrambling are excluded from the attack denominator. |
| Target switches | Changes in opponent name within each weapon's attack sequence. Same-second changes are ambiguous and excluded. Separate weapons avoid counting gun/drone interleaving as switches. |
| Engagement spans | First/last outgoing attack by opponent name, including misses. Repeated identical NPC names are combined. These spans are not time-to-kill or per-individual-NPC lifetimes. |
| Damage concentration | Largest opponent-name share and sum of squared shares, from zero to one. Repeated NPC names inflate apparent concentration. |
| Drone contribution | Currently recognizes Warrior II, observed in these logs. This is a lower bound, not complete classification. Other weapon names remain unclassified. |
| Damage types | Unavailable: no explicit EM/thermal/kinetic/explosive amounts were extracted. Do not infer actual damage splits from weapon names. |
| Personal baseline | Median of earlier loaded runs for the exact same pilot; excludes current and future runs. Duration, active DPS, incoming peak, and downtime deltas shown. Site/tier/ship/fit are not matched, so comparisons are exploratory. |

The Data coverage tab displays these limits inside the app. NPC IDs, confirmed kills, ship/fit metadata, and exact site/filament boundaries require additional evidence before supported comparisons or time-to-kill can be calculated.

Hit quality uses fixed Miss, Graze, Glance Off, Hit, Penetrate, Smash, Wreck and Unknown buckets in both directions, including zero counts. Singular/plural log labels are normalized for reports; raw events remain unchanged. No attacks gives N/A, not 0%. Low quality is Miss + Graze + Glance Off; high quality is Penetrate + Smash + Wreck. All recognized attacks, including unknown quality, stay in the denominator. Per-weapon reports make differing weapon mixes visible.

Quality baselines use the arithmetic mean of earlier same-pilot per-run rates, with each nonempty directional sample weighted equally. They show run counts, attack counts and percentage-point differences. Other metrics continue to use medians. Comparisons are descriptive and unmatched by site, tier, ship or fit.

Diagnostic observations require at least 30 attacks in the current and each prior directional sample, three prior runs, and no unknown qualities. Outgoing flags require low quality increasing by at least five percentage points and high quality falling by at least two. Similar logged damage (within 10%) and longer duration (over 10%) add context. Incoming flags require misses falling by at least five points and damage received increasing by over 20%. These are transparent product heuristics, not significance tests or causal findings. Range, transversal, ammo, exposure duration and opponent mix are investigation topics only. Predictive modeling of clear time remains deferred until comparable encounter labels and enough observations exist.
