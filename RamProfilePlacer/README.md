# RamProfilePlacer — Плагин Revit «Размещение профилей по периметру проёмов»

**Rock & Mill (РАМ Инжиниринг) · Отдел BIM · v1.0 · Revit 2026**

Плагин автоматически размещает профильные изделия (откосы, наличники) по внутреннему
периметру оконных и дверных проёмов. Сокращает операцию с часов ручного труда до секунд.

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

## Требования

| Компонент | Версия |
|---|---|
| Autodesk Revit | 2026 |
| .NET SDK | 8.0 |
| Visual Studio | 2022 (17.x) |
| OS | Windows 10/11 x64 |

---

## Пошаговая сборка

### 1. Установить необходимые программы

| # | Программа | Откуда |
|---|---|---|
| 1 | **Git** | https://git-scm.com/downloads |
| 2 | **.NET 8 SDK** | https://dotnet.microsoft.com/download/dotnet/8.0 |
| 3 | **Visual Studio 2022** (Community бесплатна) | https://visualstudio.microsoft.com — выбрать компонент «.NET desktop development» |
| 4 | **Autodesk Revit 2026** | Через Autodesk Desktop App или портал autodesk.com |

> **Примечание:** Visual Studio нужен для удобной разработки и отладки. Собрать через командную строку можно без VS, только с .NET SDK.

---

### 2. Клонировать репозиторий

```bash
git clone https://github.com/<ваш-аккаунт>/RamProfilePlacer.git
cd RamProfilePlacer
```

---

### 3. Запустить тесты (не требует Revit)

```bash
dotnet test src/RamProfilePlacer.Tests/RamProfilePlacer.Tests.csproj
```

Все тесты должны быть зелёными. Revit для этого шага **не нужен**.

---

### 4. Собрать плагин (требует установленный Revit 2026)

Revit API DLL находятся в папке установки Revit (обычно `C:\Program Files\Autodesk\Revit 2026\`).

**Вариант А — через командную строку:**

```bash
dotnet build RamProfilePlacer.sln --configuration Release /p:Platform=x64
```

Если Revit установлен не по стандартному пути:

```bash
dotnet build RamProfilePlacer.sln --configuration Release /p:Platform=x64 ^
  /p:RevitInstallPath="D:\Autodesk\Revit 2026"
```

**Вариант Б — через Visual Studio:**

1. Открыть `RamProfilePlacer.sln`
2. В менеджере конфигурации выбрать `Release | x64`
3. Нажать **Build → Build Solution** (F6)

Результат сборки: `src/RamProfilePlacer.Addin/bin/Release/net8.0-windows/RamProfilePlacer.Addin.dll`

---

### 5. Установить плагин в Revit

1. Скопируйте **все файлы** из папки сборки в удобное место, например:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2026\RamProfilePlacer\
   ```

2. Отредактируйте `src/RamProfilePlacer.Addin/RamProfilePlacer.addin`:
   - Замените `путь\к\RamProfilePlacer.Addin.dll` на полный путь к DLL

3. Скопируйте файл `RamProfilePlacer.addin` в папку манифестов Revit:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\
   ```
   Обычно это:
   ```
   C:\Users\<ваш-логин>\AppData\Roaming\Autodesk\Revit\Addins\2026\
   ```

4. Запустите Revit 2026. На вкладке **«РАМ Инжиниринг»** появится панель **«Профили»** с кнопкой **«Разместить профили»**.

---

## Использование

1. Нажмите **«Разместить профили»** на ленте.
2. В диалоге выберите семейство профиля (категория «Обобщённые модели», тип размещения WorkPlaneBased).
3. Выберите одно или несколько окон и/или дверей в модели, нажмите **Enter**.
4. Профили автоматически размещаются на внутренней грани стены. По завершении появляется отчёт.

### Требования к семейству профиля

- Категория Revit: **Обобщённые модели** (Generic Models)
- Тип размещения: **WorkPlaneBased** (на основе рабочей плоскости)
- Параметры (необязательны, но рекомендуются):
  - `ADSK_Размер_Длина` — длина профиля (тип «Длина»)
  - `Врезка в начале` — угол врезки (тип «Угол»)
  - `Врезка в конце` — угол врезки (тип «Угол»)

---

## Логи

Лог-файл создаётся ежедневно:
```
%APPDATA%\RamProfilePlacer\logs\profileplacer-YYYYMMDD.log
```

Уровни: `INFO`, `WARN`, `ERROR`.

---

## Архитектура и принцип работы

### Алгоритм определения размеров проёма

**Основной путь (EdgeLoops):**
1. Получить внутреннюю грань стены через `HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior)`.
2. Перебрать `PlanarFace.EdgeLoops`, построить BoundingBox для каждого контура в плоскости грани.
3. Внешний контур — с наибольшей площадью BB.
4. Выбрать внутренний контур, в BB которого попадает проекция точки вставки окна/двери.
5. Из BB взять ширину (нижний/верхний профиль) и высоту (левый/правый).

**Запасной путь (параметры семейства):**
Используется если грань не является PlanarFace. Даёт приближённый результат (наружный проём меньше внутреннего из-за четвертей). Факт использования записывается в лог с уровнем `WARN`.

**Проекция точки на плоскость** (явная формула, т.к. `Face.Project()` не работает над отверстиями):
```csharp
XYZ projected = point - faceNormal.Multiply((point - faceOrigin).DotProduct(faceNormal));
```

---

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`) автоматически:
- Собирает `RamProfilePlacer.Core`
- Запускает все xUnit-тесты

Проект `RamProfilePlacer.Addin` требует Revit API и собирается только локально.

---

## Лицензия

Внутренний инструмент Rock & Mill (РАМ Инжиниринг). Использование за пределами организации только с разрешения.
