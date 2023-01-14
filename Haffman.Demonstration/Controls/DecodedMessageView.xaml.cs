using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Haffman.Demonstration.Controls {
    [ContentProperty("Pairs")]
    public partial class DecodedMessageView : UserControl {
        public static readonly DependencyProperty PairsProperty;
        public static readonly DependencyProperty BrushesProperty;
        public static readonly DependencyProperty SpacingProperty;
        public static readonly DependencyProperty SelectionPenProperty;
        // _bitsIndices[charIndex] вернёт индекс диапазона битов для символа под индексом charIndex
        List<int>? _bitsIndices;
        // _rangeMap[bitsY][bitsX] вернёт индекс символа, с которым связан диапазон битов, отрисованный по этой координате
        // т.к. мы выводим не идеальный прямойгольник, то это список списков
        List<List<int>>? _charMap;
        // размеры символа
        double _formattedSymbolWidth, _formattedSymbolHeight;
        // кешируемые при отрисовке значения
        int _bitRowCount, _charRowCount, _totalBitCount;
        // массивы вершин выделений
        Point[][]? _bitSelectionVertices;
        Point[][]? _charSelectionVertices;

        // список пар символ-биты
        public List<EncodedCharPair> Pairs {
            get => (List<EncodedCharPair>)GetValue(PairsProperty);
            set => SetValue(PairsProperty, value);
        }
        // кисти для разноцветной отрисовки
        public List<Brush> Brushes {
            get => (List<Brush>)GetValue(BrushesProperty);
            set => SetValue(BrushesProperty, value);
        }
        // расстояние м-ду выводом битов и выводом символов
        public double Spacing {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }
        // перо, которым рисуем выделение
        public Pen? SelectionPen {
            get => GetValue(SelectionPenProperty) as Pen;
            set => SetValue(SelectionPenProperty, value);
        }

        static DecodedMessageView() {
            PairsProperty = DependencyProperty.Register(
                "Pairs",
                typeof(List<EncodedCharPair>),
                typeof(DecodedMessageView),
                new FrameworkPropertyMetadata(
                    new List<EncodedCharPair>(),
                    FrameworkPropertyMetadataOptions.AffectsRender));
            BrushesProperty = DependencyProperty.Register(
                "Colors",
                typeof(List<Brush>),
                typeof(DecodedMessageView),
                new FrameworkPropertyMetadata(
                    new List<Brush> {
                        new SolidColorBrush(Color.FromRgb(0xff, 0x22, 0x22)),
                        new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0xff)),
                        new SolidColorBrush(Color.FromRgb(0x22, 0xff, 0x22)),
                        new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0x22))
                    },
                    FrameworkPropertyMetadataOptions.AffectsRender));
            SpacingProperty = DependencyProperty.Register(
                "Spacing",
                typeof(double),
                typeof(DecodedMessageView),
                new FrameworkPropertyMetadata(
                    10D,
                    FrameworkPropertyMetadataOptions.AffectsRender));
            SelectionPenProperty = DependencyProperty.Register(
                "SelectionPen",
                typeof(Pen),
                typeof(DecodedMessageView),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsRender));
        }
        public DecodedMessageView() {
            InitializeComponent();
        }

        protected override void OnRender(DrawingContext context) {
            var pairs = Pairs;

            // если него рисовать, выходим
            if (pairs.Count == 0) {
                _bitsIndices = null; // заглушка для UserContorl_MouseMove

                return;
            }
            // задник
            if (Background is not null) context.DrawRectangle(Background, null,
                new Rect(0, 0, ActualWidth, ActualHeight));

            // берём кисти, если кистей нет, то создаём списо из стандартной 1-й кист Foreground,
            // используемой для текста
            var brushes = Brushes.Count > 0 ? Brushes : new List<Brush> { Foreground };
            // считаем все символы развной ширины и высоты
            var formattedSymbol = FormatString("0");
            _formattedSymbolWidth = formattedSymbol.Width;
            _formattedSymbolHeight = formattedSymbol.Height;
            // позиция выводимого символа
            var x = 0D;
            var y = 0D;
            // инициализация
            _bitsIndices = new();
            _charMap = new();
            List<int>? charMapRow = null;
            _bitRowCount = 0;
            _charRowCount = 0;
            _totalBitCount = 0;

            for (var i = 0; i < pairs.Count; i++) {
                // если цикл выполнился хоть раз, то количество строк битов равно устанавливаем в 1 и создаём строку
                if (i == 0) {
                    _bitRowCount = 1;
                    charMapRow = new();
                    _charMap.Add(charMapRow);
                }

                var pair = pairs[i];
                // если это первая пара, то её биты начинаются с 0 индекса, иначе к индексу битов прошлой пары добавляем её длину,
                // тем самым получая индекс битов текущей пары
                _bitsIndices.Add(i == 0 ? 0 : _bitsIndices[^1] + pairs[i - 1].BitCount);

                // отрисовываем биты пары
                for (var j = 0; j < pair.BitCount; j++) {
                    var formattedBit = FormatString(
                        ((pair.Bits >> j) & 0x1) != 0 ? "1" : "0", // находим значение j-го бита и преобразуем его в строку
                        brushes[i % brushes.Count]); // радуга
                    var newX = x + _formattedSymbolWidth;

                    // если след. символ будет нарисован за пределами области отрисовки, значит
                    // часть нашего символа также находится за пределами: нужно перейти на след. строку
                    // если вызодим за границу, но у нас экстремальный случай: ActualWidth < _formattedSymbolWidth, то
                    // переходим когда это на первая строка, и когда это не первый бит
                    // тогда отрисовка будет происходить как надо
                    if (newX > ActualWidth && (i != 0 || j != 0)) {
                        _bitRowCount++;
                        y += _formattedSymbolHeight;
                        charMapRow = new();
                        _charMap.Add(charMapRow);
                        newX = _formattedSymbolWidth;
                        x = 0;
                    }

                    context.DrawText(formattedBit, new Point(x, y));
                    x = newX;
                    charMapRow!.Add(i); // добавляем i, т.к. это индекс символа, на который ссылается текущий диапазон
                    _totalBitCount++;
                }
            }

            // переходим к отрисовке символов
            y += _formattedSymbolHeight + Spacing;
            x = 0;

            // аналогичный цикл
            for (var i = 0; i < pairs.Count; i++) {
                if (_charRowCount == 0) _charRowCount = 1;

                var pair = pairs[i];
                var formattedChar = FormatString(pair.Char.ToString(), brushes[i % brushes.Count]);
                var newX = x + _formattedSymbolWidth;

                if (newX > ActualWidth && i != 0) {
                    _charRowCount++;
                    y += _formattedSymbolHeight;
                    newX = _formattedSymbolWidth;
                    x = 0;
                }

                context.DrawText(formattedChar, new Point(x, y));
                x = newX;
            }

            var selectionPen = SelectionPen;

            // если текущее полодение указателя мыши не генеритует выделение, или нам его рисовать не нужно, то
            // завершаем отрисовку
            if (_bitSelectionVertices is null || selectionPen is null) return;

            // рисуем 2 выделения: для битов, и для символа
            DrawSelection(context, selectionPen, _bitSelectionVertices);
            DrawSelection(context, selectionPen, _charSelectionVertices!);
        }
        static void DrawSelection(DrawingContext context, Pen pen, Point[][] vertices) {
            context.DrawGeometry(null, pen, new PathGeometry(vertices // рисуем несколько фигур
                .Select(vertices => // преобразуем вершины фигуры в фигуру
                    new PathFigure(vertices[0], vertices
                        .Skip(1) // т.к. PathFigure сперва берёт начало, а затем мы через
                                 // LineSegment указываем продолжение, то первую вершину
                                 // пихаем в конструктор PathFigure, а с помощью остальных рисуем пуь
                        .Select(vertex => new LineSegment(vertex, true)), // true означает, что этот сегмент нужно нарисовать
                        true)))); // true означает, что эту фигуру нужно нарисовать
        }
        void UserControl_MouseMove(object sender, MouseEventArgs args) {
            if (_bitsIndices is null) return;

            // получаем позицию курсора и преобразуем её в AdjustedPosition
            var position = args.GetPosition(this);
            var ap = AdjustPosition(position);

            // если курсор не указывает на выделяемую информацию, то
            // помечаем, что выделения нет, и перерисовываем
            if (ap is null) {
                _bitSelectionVertices = null;
                _charSelectionVertices = null;
                InvalidateVisual(); // перерисовка

                return;
            }

            // вычисление вершин выделения битов переместил в отдельную функцию
            _bitSelectionVertices = ComputeBitsSelectionVertices(ap);
            // то, откуда мы начинали отрисовывать символы
            var charsOriginY = _bitRowCount * _formattedSymbolHeight + Spacing;
            // обычный прямоугольник
            // везде умножаем на _formattedSymbolXxx для преобразования координаты символа в координату вершины
            _charSelectionVertices = new[] { new[] {
                new Point(
                    ap.CharX * _formattedSymbolWidth,
                    charsOriginY + ap.CharY * _formattedSymbolHeight),
                new Point(
                    (ap.CharX + 1) * _formattedSymbolWidth,
                    charsOriginY + ap.CharY * _formattedSymbolHeight),
                new Point(
                    (ap.CharX + 1) * _formattedSymbolWidth,
                    charsOriginY + (ap.CharY + 1) * _formattedSymbolHeight),
                new Point(
                    ap.CharX * _formattedSymbolWidth,
                    charsOriginY + (ap.CharY + 1) * _formattedSymbolHeight)
            }};
            InvalidateVisual();
        }
        Point[][] ComputeBitsSelectionVertices(AdjustedPosition ap) {
            // если выледение находится внутри 1-й строчки, то это прямоугольник
            // везде умножаем на _formattedSymbolXxx для преобразования координаты символа в координату вершины
            if (ap.BitsX + ap.BitCount <= ap.ColumnCount)
                return new[] { new[] {
                    new Point(
                        ap.BitsX * _formattedSymbolWidth,
                        ap.BitsY * _formattedSymbolHeight),
                    new Point(
                        (ap.BitsX + ap.BitCount) * _formattedSymbolWidth,
                        ap.BitsY * _formattedSymbolHeight),
                    new Point(
                        (ap.BitsX + ap.BitCount) * _formattedSymbolWidth,
                        (ap.BitsY + 1) * _formattedSymbolHeight),
                    new Point(
                        ap.BitsX * _formattedSymbolWidth,
                        (ap.BitsY + 1) * _formattedSymbolHeight)
                }};
            // если выделение выходит за пределы 1-й строки, но его длина не больше длины строки, то это
            // 2 прямоугольника
            if (ap.BitCount <= ap.ColumnCount)
                return new[] {
                    new[] {
                        new Point(
                            ap.BitsX * _formattedSymbolWidth,
                            ap.BitsY * _formattedSymbolHeight),
                        new Point(
                            ap.ColumnCount * _formattedSymbolWidth,
                            ap.BitsY * _formattedSymbolHeight),
                        new Point(
                            ap.ColumnCount * _formattedSymbolWidth,
                            (ap.BitsY + 1) * _formattedSymbolHeight),
                        new Point(
                            ap.BitsX * _formattedSymbolWidth,
                            (ap.BitsY + 1) * _formattedSymbolHeight)
                    },
                    new[] {
                        new Point(
                            0,
                            (ap.BitsY + 1) * _formattedSymbolHeight),
                        new Point(
                            (ap.BitsX + ap.BitCount - ap.ColumnCount) * _formattedSymbolWidth,
                            (ap.BitsY + 1) * _formattedSymbolHeight),
                        new Point(
                            (ap.BitsX + ap.BitCount - ap.ColumnCount) * _formattedSymbolWidth,
                            (ap.BitsY + 2) * _formattedSymbolHeight),
                        new Point(
                            0 * _formattedSymbolWidth,
                            (ap.BitsY + 2) * _formattedSymbolHeight)
                    }
                };

            // иначе это сложная замкнутая ломанная, например:
            // 00000000 // 1-я строчка
            // 00000XXX // 2-я строчка
            // XXXXXXXX // 3-я строчка
            // XXXXXZ00 // 4-я строчка
            // 00000000 // 5-я строчка
            // X-м обозначаем биты, которые нужно выделить
            // Z обозначет также выделяемый бит, но координаты след. за ним символа записаны в bitsEndX/Y
            var bitsEndX = (ap.BitsX + ap.BitCount) % ap.ColumnCount;
            var bitsEndY = (ap.BitsY * ap.ColumnCount + ap.BitsX + ap.BitCount) / ap.ColumnCount;

            // 00XX  // 1-я строчка
            // XXXXF // 2-я строчка
            // E000  // 3-я строчка
            // строки - 4 бита, E - символ, на который указывают bitsEndX/Y, и если он на новой строке,
            // меняем координаты, будто он на ходится на месте несуществующего бита F
            if (bitsEndX == 0) {
                bitsEndX = ap.ColumnCount;
                bitsEndY--;
            }

            // создаём ломаную
            return new[] { new[] {
                new Point(
                    ap.BitsX * _formattedSymbolWidth,
                    ap.BitsY * _formattedSymbolHeight),
                new Point(
                    ap.ColumnCount * _formattedSymbolWidth,
                    ap.BitsY * _formattedSymbolHeight),
                new Point(
                    ap.ColumnCount * _formattedSymbolWidth,
                    bitsEndY * _formattedSymbolHeight),
                new Point(
                    bitsEndX * _formattedSymbolWidth,
                    bitsEndY * _formattedSymbolHeight),
                new Point(
                    bitsEndX * _formattedSymbolWidth,
                    (bitsEndY + 1) * _formattedSymbolHeight),
                new Point(
                    0,
                    (bitsEndY + 1) * _formattedSymbolHeight),
                new Point(
                    0,
                    (ap.BitsY + 1) * _formattedSymbolHeight),
                new Point(
                    ap.BitsX * _formattedSymbolWidth,
                    (ap.BitsY + 1) * _formattedSymbolHeight)
            }};
        }
        void UserControl_MouseLeave(object sender, MouseEventArgs e) {
            // если мышь покинула контрол, то выделять нечего, стираем выделение и перерисовываем
            _bitSelectionVertices = null;
            _charSelectionVertices = null;
            InvalidateVisual();
        }
        AdjustedPosition? AdjustPosition(in Point position) {
            // если нечего выделять, выходим
            if (_bitsIndices is null) return null;

            // преобразуем позицию курсора в координату бита
            var x = (int)(position.X / _formattedSymbolWidth);
            var y = (int)(position.Y / _formattedSymbolHeight);
            var columnCount = Math.Min((int)(ActualWidth / _formattedSymbolWidth), _totalBitCount);
            // количество бит на последней строке, которое может быть меньше чем columnCount
            var bitsRemainder = _charMap!.Last().Count;
            // charIndex < 0 если курсор находится за пределами выделяемой области
            var charIndex = -1;

            // если курсор находится в пределах битов
            if ((x < columnCount && y < _bitRowCount - 1) ||
                (x < bitsRemainder && y == _bitRowCount - 1)) {
                // то индекс символа вычисляем через мапу
                charIndex = _charMap![y][x];
            } else {
                // находим начало отрисовки символов по y
                var charsOriginY = _bitRowCount * _formattedSymbolHeight + Spacing;
                // изменяем y в соответствии с charsOriginY
                y = (int)((position.Y - charsOriginY) / _formattedSymbolHeight);
                // аналогично с bitsRemainder
                var charsRemainder = Pairs.Count % columnCount;

                if ((x < columnCount && y < _charRowCount - 1) ||
                    (x < charsRemainder && y == _charRowCount - 1)) {
                    // формула как у 2-мерного массива
                    charIndex = y * columnCount + x;
                }
            }
            if (charIndex < 0) return null;

            // находим индекс битов и их кол-во исходя из индекса символа
            var bitsIndex = _bitsIndices[charIndex];
            var bitCount = Pairs[charIndex].BitCount;

            // возвращаем результат
            return new() {
                BitsIndex = bitsIndex,
                BitsX = bitsIndex % columnCount,
                BitsY = bitsIndex / columnCount,
                CharIndex = charIndex,
                CharX = charIndex % columnCount,
                CharY = charIndex / columnCount,
                BitCount = bitCount,
                ColumnCount = columnCount
            };
        }
        FormattedText FormatString(string text, Brush? brush = null) => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            brush is null ? Foreground : brush,
            new NumberSubstitution(),
            1);

        record AdjustedPosition {
            // индекс битов
            public int BitsIndex { get; init; }
            // их положение по x
            public int BitsX { get; init; }
            // их положение по y
            public int BitsY { get; init; }
            // индекс символа
            public int CharIndex { get; init; }
            // его положение по x
            public int CharX { get; init; }
            // его положение по y
            public int CharY { get; init; }
            public int BitCount { get; init; }
            public int ColumnCount { get; init; }
        }
    }
}
