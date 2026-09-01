# Текущий baseline (с provenance)

Семь сводок сняты на финальном переносимом билде после правок
экспериментального слоя (`HeadlessTrial`, `TrialRecorder`, `HeadlessBuild`,
стенд). Физический алгоритм `Muscle` / `JointFriction` / `BalanceController`
/ `Human` намеренно не меняли. Числа отличаются от старых `damp_*` —
это новый воспроизводимый attractor после пересборки, **не** доказанное
улучшение регулятора.

Fingerprint: `5d6a30dce4daac3352512269ddbcfd57a56721634f694fd8d7a12627910cebd2`  
`buildGuid`: `aa6a9b20ca874c5aa52e5856acee055f`  
Unity 6000.5.8f1, `fixedDelta` 0.005. Фактическая сумма масс Rigidbody2D:
70.84 кг при `totalMass` 70.

Старые `damp_*` без fingerprint лежат в
`Trials/Baselines/legacy-no-provenance/`. Их не перезаписывали.
Исторические CSV — в `E:\Games\Unity3D LOGS\trials`.

`buildManifestPath` и `commandArgs` содержат абсолютные пути машины:
в fingerprint они не входят и после переноса будут другими.

## Пороги

| Метка | Что проверяет | fell | survivedSeconds | maxAbsTorsoTilt | rmsComOffset | rmsTorsoAngVel | muscleSaturationFraction |
|---|---|---|---|---|---|---|---|
| `curr_stand` | стойка 30 с | false | 30.0029 | 3.3802 | 0.0070 | 26.6462 | 0.0015 |
| `curr_act40` | `activationSpeed 40` | false | 30.0029 | 6.3800 | 0.0327 | 82.9746 | 0.2326 |
| `curr_act80` | `activationSpeed 80` | true | 7.0650 | 93.5207 | 0.5483 | 62.1328 | 0.1568 |
| `curr_pf22` | толчок **+22** Н·с | false | 20.0046 | 5.1036 | 0.0194 | 32.7054 | 0.0080 |
| `curr_pf23` | толчок **+23** Н·с | true | 6.7800 | 90.7622 | 0.7078 | 40.6393 | 0.0090 |
| `curr_pb23` | толчок **−23** Н·с | false | 20.0046 | 3.3802 | 0.0283 | 31.0803 | 0.0050 |
| `curr_pb24` | толчок **−24** Н·с | true | 6.7700 | 91.3402 | 0.7246 | 42.9462 | 0.0022 |

Вперёд держит 22, падает на 23. Назад держит 23, падает на 24.
У спокойной стойки `bothFeetGroundedFraction` = 1.
Это не эталон цельного торса из `trial-protocol.mdc`.
