using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Haffman.Demonstration.Controls {
    public partial class FrequencyTableView : UserControl {
        public HfmDictionary? Table { get; set; }

        public FrequencyTableView() {
            InitializeComponent();
        }

        protected override void OnRender(DrawingContext context) {
            // размер внутреннего отступа
            const double padding = 4;

            // задник
            context.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // если пустой словарь, значит нечего больше рисовать
            if (Table is null) return;

            // рисуем вертикальную границу, которая делит контрол по полам
            var borderPen = new Pen(BorderBrush, 1);
            context.DrawLine(borderPen,
                new Point(ActualWidth / 2, 0),
                new Point(ActualWidth / 2, ActualHeight));
            // произвольная высота строки
            var rowHeight = 24D;
            var borderY = rowHeight;
            var charText = CreateFormattedText("Символ");
            var valueText = CreateFormattedText("Частота");
            context.DrawText(charText, new Point(
                padding,
                borderY - padding - charText.Height)); // по вертикали выравниваем отн. нижней границы строки
            context.DrawText(valueText, new Point(
                ActualWidth / 2 + padding, // отрисовываем во 2-й колонке
                borderY - padding - valueText.Height));
            context.DrawLine(borderPen,
                new Point(0, borderY),
                new Point(ActualWidth, borderY)); // рисуем границу
            borderY += rowHeight;

            foreach (var (@char, info) in Table) {
                // такие же действия, что и выше, только для частот и сомволов
                charText = CreateFormattedText(@char.ToString());
                valueText = CreateFormattedText(info.Frequency.ToString());
                context.DrawText(charText, new Point(
                    padding,
                    borderY - padding - charText.Height));
                context.DrawText(valueText, new Point(
                    ActualWidth / 2 + padding,
                    borderY - padding - valueText.Height));
                context.DrawLine(borderPen,
                    new Point(0, borderY),
                    new Point(ActualWidth, borderY));
                borderY += rowHeight;
            }

        }
        FormattedText CreateFormattedText(string text) => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground,
            new NumberSubstitution(),
            1);
    }
}
