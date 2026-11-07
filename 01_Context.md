# MajorMode — контекст

**Что:** Windows desktop MVP (C# + WPF, .NET 10 LTS), генерирует картинки гитарных аккордов (аппликатура на грифе).
**Для кого:** незрячий учитель гитары (screen reader) + зрячие пользователи. Картинки вставляются в REAPER 5 → видео для учеников.
**Главный формат:** GIF (статический), доп. PNG/JPG.
**Репозиторий:** https://github.com/ggptshka-sys/majormode (пустой на старте).

## Архитектура
- `src/MajorMode.Core` — модель, парсинг/валидация, авто-диапазон ладов, геометрия, описание, имена файлов, настройки (net10.0, тестируется на любой ОС).
- `src/MajorMode.Rendering` — SkiaSharp-рендер 3 стилей + экспорт (собственный GIF-кодер, PNG/JPG через Skia) (net10.0).
- `src/MajorMode.App` — WPF MVVM, доступность UIA (net10.0-windows).
- `tests/MajorMode.Tests` — xUnit.
- `tools/MajorMode.Samples` — генерация образцов картинок.

## Ключевые решения
- Рендер на SkiaSharp (CPU) вместо WPF DrawingContext: детерминированно, без UI-потока, тестируется вне Windows; превью = тот же bitmap.
- GIF: свой кодер (median cut + зарезервированные цвета маркеров + Floyd–Steinberg + LZW).
- Среда разработки агента — macOS: WPF только компилируется, не запускается.

## Состояние
См. `02_Plan.md`.
