# RamProfilePlacer — Плагин Revit «Размещение профилей по периметру проёмов»

**Rock & Mill (РАМ Инжиниринг) · Отдел BIM · v1.1 · Revit 2026**

Плагин автоматически размещает профильные изделия (откосы, наличники) по внутреннему периметру оконных и дверных проёмов. Сокращает операцию с часов ручного труда до секунд.

---

## Содержание

- [Структура репозитория](#структура-репозитория)
- [Системные требования](#системные-требования)
- [Сборка](#сборка)
- [Установка](#установка)
- [Использование](#использование)
- [Требования к семейству профиля](#требования-к-семейству-профиля)
- [Архитектура](#архитектура)
- [Описание модулей](#описание-модулей)
- [Алгоритм работы](#алгоритм-работы)
- [Логирование](#логирование)
- [Тестирование](#тестирование)
- [CI/CD](#cicd)
- [Известные ограничения](#известные-ограничения)

---

## Структура репозитория

```
RamProfilePlacer.sln
└─ src/
   ├─ RamProfilePlacer.Core/          ← чистая бизнес-логика, без Revit API
   │  ├─ Geometry/
   │  │  ├─ Point2D.cs
   │  │  ├─ Point3D.cs
   │  │  ├─ BoundingBox2D.cs
   │  │  ├─ OpeningSide.cs
   │  │  ├─ OpeningRectangle.cs
   │  │  └─ EdgeLoopAnalyzer.cs
   │  ├─ Model/
   │  │  ├─ ProfilePlacement.cs
   │  │  └─ OpeningDimensions.cs
   │  └─ ProfileLayoutCalculator.cs
   │
   ├─ RamProfilePlacer.Addin/          ← адаптер Revit (требует RevitAPI.dll)
   │  ├─ Application.cs               (IExternalApplication, лента)
   │  ├─ Commands/PlaceProfilesCommand.cs  (IExternalCommand)
   │  ├─ Revit/
   │  │  ├─ WallFaceReader.cs
   │  │  ├─ OpeningCollector.cs
   │  │  ├─ FamilyPlacer.cs
   │  │  └─ WarningSwallower.cs
   │  ├─ UI/ProfileSelectDialog.xaml(.cs)
   │  ├─ Infrastructure/FileLogger.cs
   │  └─ RamProfilePlacer.addin       (манифест)
   │
   └─ RamProfilePlacer.Tests/          ← xUnit, не требует Revit
      ├─ BoundingBox2DTests.cs
      ├─ EdgeLoopAnalyzerTests.cs
      └─ ProfileLayoutCalculatorTests.cs
```

---

## Системные требования

| Компонент | Версия |
|---|---|
| Autodesk Revit | 2026 |
| .NET SDK | 8.0 |
| Visual Studio | 2022 (17.x) |
| ОС | Windows 10 / 11 x64 |

---

## Сборка

### Тесты (не требуют Revit)

```bash
dotnet test src/RamProfilePlacer.Tests/RamProfilePlacer.Tests.csproj
```

### Через командную строку

```bash
dotnet build RamProfilePlacer.sln --configuration Release /p:Platform=x64
```

Если Revit установлен не по стандартному пути:

```bash
dotnet build RamProfilePlacer.sln --configuration Release /p:Platform=x64 ^
  /p:RevitInstallPath="D:\Autodesk\Revit 2026"
```

### Через Visual Studio 2022

1. Открыть `RamProfilePlacer.sln`
2. Конфигурация: `Release | x64`
3. **Build → Build Solution** (F6)

Результат сборки:
```
src/RamProfilePlacer.Addin/bin/Release/net8.0-windows/RamProfilePlacer.Addin.dll
src/RamProfilePlacer.Addin/bin/Release/net8.0-windows/RamProfilePlacer.Core.dll
```

---

## Установка

1. Скопируйте все файлы из папки сборки в удобное место, например:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2026\RamProfilePlacer\
   ```

2. Отредактируйте `RamProfilePlacer.addin` — замените `путь\к\RamProfilePlacer.Addin.dll` на полный путь к DLL.

3. Скопируйте `RamProfilePlacer.addin` в папку манифестов Revit:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\
   ```

4. Запустите Revit 2026. На вкладке **«РАМ Инжиниринг»** появится панель **«Профили»** с кнопкой **«Разместить профили»**.

---

## Использование

### Шаг 1 — Подготовка семейства

Загрузите в проект Revit семейство профиля. Требования к семейству — в следующем разделе.

### Шаг 2 — Запуск команды

Нажмите **«Разместить профили»** на ленте → вкладка **РАМ Инжиниринг** → панель **Профили**.

### Шаг 3 — Выбор семейства

В диалоге выберите нужный тип профиля из списка (`«ИмяСемейства – ИмяТипа»`) и нажмите **ОК**.

Список содержит только семейства категории «Обобщённые модели» с типом размещения `WorkPlaneBased`.

### Шаг 4 — Выбор проёмов

Выберите в модели одно или несколько окон и/или дверей, затем нажмите **Enter**.

### Шаг 5 — Результат

По завершении отображается отчёт:
- количество размещённых профилей;
- список пропущенных проёмов с причиной (дуговые стены, ошибки геометрии и т.п.).

**Количество профилей на проём:**
- Окно: 4 шт. (нижний + верхний + левый + правый).
- Дверь: 3 шт. (верхний + левый + правый; нижний не требуется).

---

## Требования к семейству профиля

| Требование | Значение |
|---|---|
| Категория Revit | Обобщённые модели (`OST_GenericModel`) |
| Тип размещения | `WorkPlaneBased` (на основе рабочей плоскости) |
| Параметр длины | `ADSK_Размер_Длина` (тип «Длина», изменяемый) |
| Параметр врезки начала | `Врезка в начале` (тип «Угол», изменяемый, в радианах) |
| Параметр врезки конца | `Врезка в конце` (тип «Угол», изменяемый, в радианах) |

Параметры `Врезка в начале` и `Врезка в конце` необязательны — если они отсутствуют, плагин выведет предупреждение в лог и продолжит работу. `ADSK_Размер_Длина` необходим для корректной длины профиля.

Угол врезки фиксирован: **45°** (π/4 рад) на обоих концах.

---

## Архитектура

### Слои и зависимости

```
RamProfilePlacer.Addin   (Revit API, WPF)
    └── RamProfilePlacer.Core   (нет внешних зависимостей)

RamProfilePlacer.Tests
    └── RamProfilePlacer.Core
```

`Core` полностью изолирован от Revit API: его можно собирать и тестировать без установленного Revit.

### Поток данных

```
Revit API
  ↓ Wall.GetSideFaces → PlanarFace + Reference
  ↓ EdgeLoops → Point2D[]
  ↓ WallFaceReader (Addin)
  ↓ OpeningDimensions (Core)
  ↓ ProfileLayoutCalculator.Calculate (Core)
  ↓ ProfilePlacement[] (Core)
  ↓ FamilyPlacer (Addin)
  ↓ doc.Create.NewFamilyInstance → FamilyInstance
```

---

## Описание модулей

### RamProfilePlacer.Core

#### `Geometry/Point2D`

Структура двумерной точки в системе координат грани стены (оси U/V).

| Член | Описание |
|---|---|
| `U`, `V` | Координаты |
| `+`, `-`, `*` | Арифметика векторов |
| `DistanceTo(Point2D)` | Евклидово расстояние |

#### `Geometry/Point3D`

Структура трёхмерной точки без зависимости от Revit.

| Член | Описание |
|---|---|
| `X`, `Y`, `Z` | Координаты |
| `+`, `-`, `*` | Арифметика векторов |
| `DotProduct(Point3D)` | Скалярное произведение |
| `CrossProduct(Point3D)` | Векторное произведение |
| `Length` | Длина вектора |
| `Normalize()` | Единичный вектор |
| `ProjectOnPlane(origin, normal)` | Проекция точки на плоскость |

#### `Geometry/BoundingBox2D`

Ограничивающий прямоугольник в плоскости грани (U/V).

| Член | Описание |
|---|---|
| `MinU`, `MaxU`, `MinV`, `MaxV` | Границы |
| `Width`, `Height`, `Area` | Производные размеры |
| `Center` | Центральная точка |
| `FromPoints(IEnumerable<Point2D>)` | Строит BBox из набора точек |
| `Contains(Point2D, tolerance)` | Принадлежность точки (с допуском 1e-6) |
| `GetCorners()` | Четыре угловые точки |

#### `Geometry/OpeningRectangle`

Внутренний прямоугольник проёма в координатах U/V грани: ширина, высота, четыре угловые точки. Создаётся из `BoundingBox2D`.

#### `Geometry/OpeningSide`

Перечисление сторон проёма: `Bottom`, `Top`, `Left`, `Right`.

#### `Geometry/EdgeLoopAnalyzer`

Анализирует набор контуров `EdgeLoops` грани и находит прямоугольник нужного проёма.

| Метод | Описание |
|---|---|
| `FindOpeningRectangle(loops, openingProjection)` | Выбирает внутренний контур по точке вставки |
| `FindOuterLoop(loops)` | Контур с наибольшей площадью BBox |
| `FindInnerLoops(loops)` | Все контуры кроме внешнего |

Алгоритм `FindOpeningRectangle`:
1. Находит внешний контур (наибольшая площадь BB).
2. Среди внутренних контуров ищет тот, BB которого содержит `openingProjection`.
3. Если ни один BB не содержит точку — выбирает ближайший по расстоянию от центра BB (fallback внутри Core).

#### `Model/OpeningDimensions`

Геометрия внутреннего проёма: ширина, высота, 3D-координаты четырёх углов.

| Свойство | Описание |
|---|---|
| `Width` | Ширина (горизонталь, в футах) |
| `Height` | Высота (вертикаль, в футах) |
| `BottomLeft3D` / `BottomRight3D` / `TopLeft3D` / `TopRight3D` | Углы в мировых координатах |
| `IsExact` | `true` — EdgeLoops (точные), `false` — параметры семейства (приблизительные) |

#### `Model/ProfilePlacement`

Данные для размещения одного экземпляра профиля.

| Свойство | Описание |
|---|---|
| `Side` | Сторона проёма |
| `InsertionPoint` | Точка вставки (начало профиля) |
| `Direction` | Единичный направляющий вектор |
| `Length` | Длина профиля (в футах) |

Конструктор выбрасывает `ArgumentException` при `Length <= 0`.

#### `ProfileLayoutCalculator`

Статический класс. Метод `Calculate(dims, isDoor)` возвращает `IReadOnlyList<ProfilePlacement>`.

| Сторона | Точка вставки | Направление | Длина |
|---|---|---|---|
| Bottom (только окно) | BottomLeft | → BottomRight | Width |
| Top | TopLeft | → TopRight | Width |
| Left | BottomLeft | → TopLeft | Height |
| Right | BottomRight | → TopRight | Height |

---

### RamProfilePlacer.Addin

#### `Application`

Точка входа плагина (`IExternalApplication`). При загрузке Revit:
- создаёт вкладку **РАМ Инжиниринг** (если её нет — перехватывает `Autodesk.Revit.Exceptions.InvalidOperationException`);
- добавляет панель **Профили** с кнопкой команды.

#### `Commands/PlaceProfilesCommand`

Основная команда (`IExternalCommand`, `TransactionMode.Manual`).

Порядок выполнения:
1. Собирает `FamilySymbol` (GenericModel + WorkPlaneBased).
2. Показывает диалог выбора символа.
3. Запускает `PickObjects` с `OpeningSelectionFilter`.
4. Открывает транзакцию Revit с `WarningSwallower`.
5. Для каждого проёма: получает грань → вычисляет размеры → раскладывает профили.
6. Коммитит транзакцию, показывает отчёт.

При ошибке отдельного проёма (`NotSupportedException` или любое другое исключение) проём пропускается с записью в отчёт — остальные проёмы обрабатываются в той же транзакции.

#### `Revit/OpeningCollector` (OpeningSelectionFilter)

`ISelectionFilter` — разрешает выбирать только окна (`OST_Windows`) и двери (`OST_Doors`).

#### `Revit/WallFaceReader`

Статический класс. Читает внутреннюю грань стены и определяет размеры проёма.

**`GetInteriorFace(Wall)`**

Возвращает `(Face face, Reference faceRef)` внутренней грани.

> **Важно:** `face.Reference` всегда `null`. Для `NewFamilyInstance` необходимо передавать `faceRef`, полученный из `HostObjectUtils.GetSideFaces` — именно он сохраняется здесь.

**`GetOpeningDimensions(face, openingLocation, opening, out usedFallback, logger?)`**

Определяет размеры проёма. Использует два пути:

| Путь | Условие | Точность |
|---|---|---|
| EdgeLoops (основной) | Грань является `PlanarFace`, анализ контуров успешен | `IsExact = true` |
| Параметры семейства (запасной) | Грань не `PlanarFace` **или** EdgeLoop-анализ выбросил исключение | `IsExact = false` |

При переходе на запасной путь в лог (через переданный `logger`) записывается тип и сообщение исходного исключения — для диагностики.

> **Критический нюанс:** `WINDOW_WIDTH`/`WINDOW_HEIGHT` и `DOOR_WIDTH`/`DOOR_HEIGHT` — параметры *типа* (type parameters). Они читаются из `fi.Symbol`, а не из экземпляра `fi` — у экземпляра эти параметры возвращают `null`.

**`ProjectOnPlane(point, faceOrigin, faceNormal)`**

Явная проекция точки на плоскость (формулой через скалярное произведение). Используется вместо `Face.Project()` — тот не работает над отверстиями в грани.

#### `Revit/FamilyPlacer`

Размещает один профиль через `doc.Create.NewFamilyInstance(faceRef, location, refDir, symbol)`.

После размещения устанавливает три параметра:

| Параметр | Значение | Тип Revit |
|---|---|---|
| `ADSK_Размер_Длина` | Длина профиля | Length (футы) |
| `Врезка в начале` | π/4 (45°) | Angle (радианы) |
| `Врезка в конце` | π/4 (45°) | Angle (радианы) |

Отсутствующие или read-only параметры логируются как `WARN`, ошибкой не являются.

#### `Revit/WarningSwallower`

`IFailuresPreprocessor` — подавляет предупреждения (`FailureSeverity.Warning`) в транзакции. Ошибки уровня `Error` не трогает.

#### `UI/ProfileSelectDialog`

WPF-диалог выбора семейства. Показывает список в формате `«ИмяСемейства – ИмяТипа»`, отсортированный по имени семейства, затем по имени типа. Кнопка **ОК** активируется только при наличии выбранного элемента.

#### `Infrastructure/FileLogger`

Потокобезопасный текстовый логгер (Singleton, `lock` на запись).

| Деталь | Значение |
|---|---|
| Путь | `%APPDATA%\RamProfilePlacer\logs\profileplacer-YYYYMMDD.log` |
| Формат строки | `[HH:mm:ss.fff] [LEVEL] message` |
| Уровни | `INFO`, `WARN`, `ERROR` |
| Ротация | По дате (один файл в день) |

---

## Алгоритм работы

### Полная цепочка

```
1. Wall → HostObjectUtils.GetSideFaces(Interior)
              → PlanarFace  +  faceRef (Reference)

2. PlanarFace.EdgeLoops
      → тесселяция рёбер (Edge.Tessellate)
      → Point2D[] в плоскости грани (U/V)
      → List<FaceLoop>

3. FamilyInstance.Location → LocationPoint → openingLocation (XYZ)
      → ProjectOnPlane → projectedUV (Point2D)

4. EdgeLoopAnalyzer.FindOpeningRectangle(loops, projectedUV)
      → BoundingBox2D внутреннего контура
      → OpeningRectangle (углы в U/V)

5. Обратное преобразование углов: 2D → 3D
      p3D = faceOrigin + faceU * u + faceV * v

6. OpeningDimensions (IsExact = true)

7. ProfileLayoutCalculator.Calculate(dims, isDoor)
      → List<ProfilePlacement>

8. foreach ProfilePlacement:
      FamilyPlacer.PlaceProfile(doc, symbol, faceRef, placement)
          → NewFamilyInstance(faceRef, insertionPoint, direction, symbol)
          → SetParameter("ADSK_Размер_Длина", length)
          → SetParameter("Врезка в начале", π/4)
          → SetParameter("Врезка в конце", π/4)
```

### Запасной путь (если EdgeLoops недоступны)

```
FamilyInstance.Symbol.get_Parameter(WINDOW_WIDTH / DOOR_WIDTH)   ← параметр типа
FamilyInstance.Symbol.get_Parameter(WINDOW_HEIGHT / DOOR_HEIGHT)
    → width, height

fi.GetTransform().BasisX → right (горизонталь)
XYZ.BasisZ               → up    (вертикаль)
(fi.Location as LocationPoint).Point → loc (точка вставки = нижний центр проёма)

Углы:
    BottomLeft  = loc − right*(width/2)
    BottomRight = loc + right*(width/2)
    TopLeft     = loc − right*(width/2) + up*height
    TopRight    = loc + right*(width/2) + up*height

OpeningDimensions (IsExact = false)
```

---

## Логирование

Пример лог-файла:

```
[10:15:32.441] [INFO] === PlaceProfilesCommand started ===
[10:15:33.012] [INFO] Selected family: 'Профиль_Откос', type: '70мм' (ElementId=123456)
[10:15:33.104] [INFO] Processing opening ElementId=234567, category=Окна
[10:15:33.201] [INFO]   → Placed 4 profiles for ElementId=234567.
[10:15:33.310] [INFO] Processing opening ElementId=345678, category=Двери
[10:15:33.311] [WARN]   EdgeLoop analysis failed for ElementId=345678, switching to family-parameter fallback. Reason: InvalidOperationException: Not enough loops to identify opening (need at least 2).
[10:15:33.318] [WARN]   Fallback used for ElementId=345678. Sizes taken from family parameters — please verify manually.
[10:15:33.325] [INFO]   → Placed 3 profiles for ElementId=345678.
[10:15:33.350] [INFO] === PlaceProfilesCommand finished: placed=7, skipped=0 ===
```

---

## Тестирование

Тесты не зависят от Revit API и запускаются без установленного Revit:

```bash
dotnet test src/RamProfilePlacer.Tests/RamProfilePlacer.Tests.csproj
```

### Покрытие тестами

**`BoundingBox2DTests`**
- Создание из точек, корректность границ
- Width / Height / Area
- Contains (внутри, снаружи, на границе, за пределами допуска)
- GetCorners — четыре угловые точки
- Конструктор с некорректными аргументами

**`EdgeLoopAnalyzerTests`**
- `FindOuterLoop` — возвращает контур с наибольшей площадью BBox
- `FindInnerLoops` — исключает внешний контур
- `FindOpeningRectangle` — один внутренний контур; два внутренних контура (выбор по проекции левого/правого); вырожденные данные (0 и 1 контур)
- `OpeningRectangle` — соответствие Width ↔ горизонталь, Height ↔ вертикаль

**`ProfileLayoutCalculatorTests`**
- Количество профилей: 4 для окна, 3 для двери (без Bottom)
- Стороны: все четыре у окна, Top+Left+Right у двери
- Длины: Bottom/Top = Width, Left/Right = Height
- Точки вставки для каждой стороны
- Направляющие векторы — единичная длина
- Направления: Bottom = +X, Left = +Z
- Смещённый проём (origin ≠ 0)
- `ProfilePlacement` с отрицательной длиной → `ArgumentException`
- `Point3D.ProjectOnPlane` — три сценария (точка над плоскостью, на плоскости, наклонная плоскость)

---

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`) при каждом push и PR в ветку `main`:
- Собирает `RamProfilePlacer.Core`
- Запускает все xUnit-тесты

Проект `RamProfilePlacer.Addin` требует Revit API и собирается только локально.

---

## Известные ограничения

| Ограничение | Описание |
|---|---|
| Дуговые стены | Проёмы в стенах с `LocationCurve != Line` пропускаются с сообщением в отчёте. |
| Сложные грани | Используется первая внутренняя грань (`GetSideFaces[0]`). Многослойные конструкции с несколькими гранями не тестировались. |
| Запасной путь (fallback) | При невозможности анализа EdgeLoops координаты углов вычисляются из параметров семейства и могут не учитывать четверти. Рекомендуется ручная проверка (фиксируется в логе с уровнем `WARN`). |
| Угол врезки | Фиксирован 45°. При разной ширине профилей на смежных сторонах точный стык не обеспечивается. |
| Тип семейства | Только `WorkPlaneBased`. Семейства `FaceBased` и `TwoLevelsBased` в список не попадают. |

---

## Лицензия

Внутренний инструмент Rock & Mill (РАМ Инжиниринг). Использование за пределами организации только с разрешения.
