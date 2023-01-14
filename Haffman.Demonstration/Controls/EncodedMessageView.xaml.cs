using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Haffman.Demonstration.Controls {
    using static Math; // Min/Max
    // чтобы удобно заполнять пары через разметку
    [ContentProperty("Pairs")]
    // отображает как каждый символ был закодирован
    public partial class EncodedMessageView : UserControl {
        // WPF'овская дичь, чтобы работало
        public static readonly DependencyProperty PairsProperty;
        public static readonly DependencyProperty MaxBitCountProperty;
        public static readonly DependencyProperty CellSpacingProperty;
        public static readonly DependencyProperty CellsPerRowProperty;

        // пары символ-биты
        public List<EncodedCharPair> Pairs {
            get => (List<EncodedCharPair>)GetValue(PairsProperty);
            set => SetValue(PairsProperty, value);
        }
        // след. св-ва берут и форматируют вывод
        // для выравнивания
        public int MaxBitCount {
            get => (int)GetValue(MaxBitCountProperty);
            set => SetValue(MaxBitCountProperty, value);
        }
        // интервал и дистанция м-ду парами
        public Size CellSpacing {
            get => (Size)GetValue(CellSpacingProperty);
            set => SetValue(CellSpacingProperty, value);
        }
        // кол-во пар на 1 строчку
        // если 0, то встраивается в ширину
        public int CellsPerRow {
            get => (int)GetValue(CellsPerRowProperty);
            set => SetValue(CellsPerRowProperty, value);
        }

        // WPF stuff
        static EncodedMessageView() {
            PairsProperty = DependencyProperty.Register(
                "Pairs",
                typeof(List<EncodedCharPair>),
                typeof(EncodedMessageView),
                new FrameworkPropertyMetadata(
                    new List<EncodedCharPair>(),
                    FrameworkPropertyMetadataOptions.AffectsRender));
            MaxBitCountProperty = DependencyProperty.Register(
                "BitCount",
                typeof(int),
                typeof(EncodedMessageView),
                new FrameworkPropertyMetadata(
                    8,
                    FrameworkPropertyMetadataOptions.AffectsRender));
            CellSpacingProperty = DependencyProperty.Register(
                "CellSpacingProperty",
                typeof(Size),
                typeof(EncodedMessageView),
                new FrameworkPropertyMetadata(
                    new Size(10, 10),
                    FrameworkPropertyMetadataOptions.AffectsRender));
            CellsPerRowProperty = DependencyProperty.Register(
                "CellsPerRow",
                typeof(int),
                typeof(EncodedMessageView),
                new FrameworkPropertyMetadata(
                    0,
                    FrameworkPropertyMetadataOptions.AffectsRender));
        }
        public EncodedMessageView() {
            InitializeComponent();
        }

        // отрисовка
        protected override void OnRender(DrawingContext context) {
            // рисуем задний фон
            if (Background is not null)
                context.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // координаты выводимого текста
            var x = 0D;
            var y = 0D;
            // y-координата линии текста
            var lineY = 0D;
            // высота линии (вычистяется по ходу отрисовки)
            var lineHeight = 0D;

            for (var i = 0; i < Pairs.Count; i++) {
                var pair = Pairs[i];
                // формататирование (вывести можно только FormattedText)
                var formattedChar = CreateFormattedText(pair.Char.ToString());
                var formattedBits = CreateFormattedText(
                    FormatBits(pair.Bits, pair.BitCount)
                    .PadRight(MaxBitCount)); // .PadRight - функция выравнивания
                // добавляем ширину самой широкой строчки к x + интервал
                var newX = x + Max(formattedChar.Width, formattedBits.WidthIncludingTrailingWhitespace) + CellSpacing.Width;
                
                // если у нас количество равно 0, и мы вышли за пределы ширины + это не перввая итерация, тогда
                if (CellsPerRow == 0 && newX > ActualWidth && i != 0) {
                    // возвращаем x в 0-ю позицию
                    newX -= x;
                    x = 0;
                    // сдвигаемся на 1 строчку вниз
                    lineY = lineY + lineHeight + CellSpacing.Height;
                    y = lineY;
                }

                // вывод
                context.DrawText(formattedChar, new Point(x, y));
                y += formattedChar.Height; // выводим друг под дружкой
                context.DrawText(formattedBits, new Point(x, y));
                // обновляем высоту линии
                lineHeight = Max(lineHeight, formattedChar.Height + formattedBits.Height);

                // когда мы отрисовали строку из пар, при этом важно, что
                // CellsPerRow != 0, т.к. обработчик, каогда оно равно 0, находится выше
                if (CellsPerRow != 0 && i % CellsPerRow == CellsPerRow - 1) {
                    // высота линии + диста
                    lineY += lineHeight + CellSpacing.Height;
                    // x в исходное
                    x = 0;
                }

                // двигаем x
                x = newX;
                // возвращаем y в исходное
                y = lineY;
            }
        }
        // string -> FormattedText
        FormattedText CreateFormattedText(string text) => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Brushes.White,
            new NumberSubstitution(),
            1);
        // биты -> string
        string FormatBits(long bits, int bitCount) {
            Span<char> buffer = stackalloc char[bitCount];

            for (var i = 0; i < bitCount; i++)
                buffer[i] = ((bits >> (bitCount - 1 - i)) & 0x1) == 0 ? '0' : '1';

            return new string(buffer);
        }
    }
}
