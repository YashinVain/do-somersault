# Модели из Blender

Все модели игры собираются скриптами в Blender (5.2) и лежат в `Assets/_Project/Models`. Скрипты в папке `Blender/`
можно запускать заново и менять: после запуска FBX обновляется, а сцену в Unity надо пересобрать
(меню Jump Game → Build Prototype Scene).

## Что где

| Файл | Что делает | Результат |
|------|-----------|-----------|
| `Blender/make_diver.py` | человек: тело, одежда и всё снаряжение героев | `Models/Characters/Diver.fbx`, `Blender/Diver.blend` |
| `Blender/make_robot.py` | робот со своим телом на тех же костях | `Models/Characters/Robot.fbx` |
| `Blender/make_nature.py` | деревья, сосны, пальмы, камни, скальные столбы, уступы | `Models/Props/Trees.fbx`, `Rocks.fbx`, `Cliffs.fbx` |
| `Blender/make_houses.py` | дома Старого города (по модели на каждый размер) | `Models/Props/Houses.fbx` |
| `Blender/lib.py`, `Blender/nature.py` | общие инструменты: профили, кости, веса, шум, экспорт | |

Запуск (путь к Blender свой):

```bash
"C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe" --background --factory-startup --python Blender/make_diver.py
```

Картинки для проверки скрипты кладут в `Blender/previews`.

## Герои

Модель надета на тот же скелет, который двигает код (приседание, группировка, полёт), поэтому физика, очки и медали
от внешнего вида не зависят. Кости копируют повороты суставов игры, оси костей в Blender не важны.

**Имена костей** (обязательные): `Hips`, `Spine`, `Head`, `Arm_L`, `Arm_R`, `ForeArm_L`, `ForeArm_R`,
`Thigh_L`, `Thigh_R`, `Shin_L`, `Shin_R`, `Ankle_L`, `Ankle_R`. `Head` необязательна.

**Положение суставов** в позе покоя (метры, Unity: x вправо, y вверх, z вперёд, начало в бёдрах, персонаж смотрит на +Z):

| Сустав | x | y | z |
|--------|---|---|---|
| Spine | 0 | 0,10 | 0 |
| Head | 0 | 0,73 | 0,01 |
| Arm_L / Arm_R | ∓0,235 | 0,50 | 0 |
| ForeArm | ∓0,235 | 0,23 | 0 |
| Thigh | ∓0,10 | 0,02 | 0 |
| Shin (колено) | ∓0,10 | −0,42 | 0 |
| Ankle | ∓0,10 | −0,845 | 0 |

Поза покоя: руки опущены вниз, ноги прямые. Подошва на высоте −0,875, носок на z +0,16, пятка на z −0,08.
Рост около 1,77 м. В Blender персонаж смотрит в −Y, Z вверх. Левая сторона персонажа — это +X в Blender.

**Материалы-роли.** Игра красит материалы по имени (суффиксы `.001` не мешают):
`Skin`, `Face`, `Outfit` (плавки), `Shirt`, `Cap`, `Strap`, `Lens`, `Hair`, `Accent`, `Metal`.
Остальные материалы (например `EyeWhite`, `Mouth`) остаются как нарисованы.

**Снаряжение.** Объект с именем `Gear_<Название>` показывается, если у героя есть это снаряжение; несколько названий
через `_` значат «любое из них». Объект `NoGear_<Название>` наоборот прячется, когда снаряжение есть (нога под протезом).
Названия: `SwimCap`, `Goggles`, `Eyes`, `Hair`, `Beard`, `Ponytail`, `Shirt`, `Backpack`, `Tank`, `SwimRing`, `Antenna`,
`PirateHat`, `Eyepatch`, `Sunglasses`, `Mask`, `Blade`.

**Толстяк.** Shape key `Fat` на каждой сетке, авторская ширина +50 %; игра подмешивает его по ширине героя.

**Новый герой со своей моделью.** Экспортируйте FBX с теми же костями в `Models/Characters/Имя.fbx`,
добавьте его в список в `BuildDiver` (`PrototypeSceneBuilder.cs`) и укажите `model: "Имя"` у героя в `Skins.cs`.

## Настройки экспорта FBX из Blender

Forward: −Z, Up: Y, Apply Scalings: FBX Units Scale, Add Leaf Bones: выключено, Only Deform Bones: выключено.
Для объектов без скелета включите Apply Transform. Unity настраивает импорт сам (`Editor/ModelImport.cs`):
без анимации, без оптимизации костей, с blend shapes.

## Окружение

Материалы называются по назначению: `Crown`, `Bark`, `Needles`, `PalmLeaf`, `PalmTrunk`, `Coconut`, `LeafA`, `LeafB`,
`Rock0`–`Rock2`, `Cap`, `Top`, `Under`; у домов `Plaster`, `Trim`, `Roof`, `Glass`, `Shutter`, `Door`, `Leaf`, `Flower`.
Сборщик сцены (`PrototypeSceneBuilder.Props.cs`) берёт модель и красит каждый слот нужным цветом. Если файла модели нет,
сборщик строит прежние простые формы, так что игра не ломается.

Модели лежат на своих местах и подходят к физике:
- камни `Rock_A…D` с радиусом 0,5, чтобы работали прежние масштабы;
- столбы `Pillar_A/B` высотой 1 и радиусом основания 1, сборщик растягивает их по размеру;
- уступы `Ledge_A/B` с началом на краю стены, верх на нуле;
- дома `House_<ширина>_<высота>` шириной × 9 м глубиной, фасад к +Z, центр основания в начале.

## Проверка

`DiveCapture.BuildAndCapturePoses` снимает героев в разных позах, `BuildAndCaptureQuick` собирает сцену и снимает
экраны и локации. Результат в `Logs/`.
