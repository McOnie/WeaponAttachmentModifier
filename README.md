# Weapon Attachment Modifier

***

Easily configure a number of stats for weapon mods/attachments.

Currently the config allows for editing Ergonomics, Recoil Reduction and Durability Burn (for muzzle devices), with plans to add more configurable options.

***

## Multiplier Information
### Ergonomics Multiplier
- Items with default positive ergo (buff) is multiplied by this value ( e.g. +10 ergo * 2 = +20 ergo )
- Items with default negative ergo (penalty) is divided by this value ( e.g. -10 ergo / 2 = -5 ergo )
- Multiplier > 1 increases buff and decreases penalty ( e.g. 1.5: +10 * 1.5 = +15 ). Default is 1
- Multiplier < 1 decreases buff and increases penalty ( e.g. 0.5: +10 * 0.5 = +5 ). Default is 1

### Recoil Multiplier
- Multiplier > 1 increases reduction ( e.g. 1.5: -20% * 1.5 = 30% ). Default is 1
- Multiplier < 1 decreases reduction ( e.g. 0.5: -20% * 0.5 = 10% ). Default is 1

### Durability Burn Multiplier
- Multiplier > 1 increases burn rate ( e.g. 1.5: +50% * 1.5 = +75% ). Default is 1
- Multiplier < 1 decreases burn rate ( e.g. 0.5: +50% * 0.5 = +25% ). Default is 1