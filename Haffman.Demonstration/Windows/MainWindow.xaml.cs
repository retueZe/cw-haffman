using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Haffman.Demonstration.Controls;

namespace Haffman.Demonstration {
    using HfmDictionaryNodeBase = HfmDictionaryNode;
    using HfmDictionaryNode = HfmDictionaryNode.Node;
    using HfmDictionaryLeaf = HfmDictionaryNode.Leaf;
    using static Math;
    public partial class MainWindow : Window {
        // таймер, по истечению которого обновляется содержимое секции вывода
        readonly DispatcherTimer _encodeTimer = new() {
            Interval = TimeSpan.FromSeconds(1) // интервал в 1 секунду
        };
        // словарь
        HfmDictionary? _dictionary;

        // WPF stuff
        public MainWindow() {
            InitializeComponent();
        }

        // когда окно закрузилось, все его св-ва и контролы инициализированы
        void Window_Loaded(object sender, RoutedEventArgs args) {
            // обработчики событий для чекбокса
            areInputsBoundInput.Checked += areInputsBoundInput_Checked;
            areInputsBoundInput.Unchecked += areInputsBoundInput_Unchecked;
            // если мы изменили шаблон, словарь станет недействительным. добавляем обработчик сюда
            templateInput.TextChanged += templateInput_TextChanged;
            // обработчик, который позволяет менять текс текстбокса сообщения вместе с текстом текстбокса шаблона
            templateInput.TextChanged += BoundInputTextChangedCallback;
            // если изменить 1 из текстбоксов, то запустится таймер по обновлению вывода
            templateInput.TextChanged += EncodeTimerRestartCallback;
            messageInput.TextChanged += EncodeTimerRestartCallback;
            // обработчик, который указывает таймеру, что дулать при срабатывании (перекодировать сообщение)
            _encodeTimer.Tick += encodeTimer_Tick;
        }
        void templateInput_TextChanged(object sender, TextChangedEventArgs args) {
            _dictionary = null;
        }
        void encodeTimer_Tick(object? sender, EventArgs args) {
            // останавливаем таймер, чтобы не перекодироваться каждую секунду
            _encodeTimer.Stop();
            Encode(); // кодируем
        }
        void areInputsBoundInput_Checked(object sender, RoutedEventArgs args) {
            // отключаем для пользователя текстбокс сообщения
            messageInput.IsEnabled = false;
            // копируем в него наш шаблон
            messageInput.Text = templateInput.Text;
            // добавляем синхронизацию м-ду текстбоксами
            templateInput.TextChanged += BoundInputTextChangedCallback;
        }
        // ^^^ отменяем действие прошлого хандлера ^^^
        void areInputsBoundInput_Unchecked(object sender, RoutedEventArgs args) {
            messageInput.IsEnabled = true;
            templateInput.TextChanged -= BoundInputTextChangedCallback;
        }
        void BoundInputTextChangedCallback(object sender, TextChangedEventArgs args) {
            // просто копируем текст из текстбокса шаблона в текстбокс сообщения
            messageInput.Text = templateInput.Text;
        }
        void EncodeTimerRestartCallback(object sender, TextChangedEventArgs args) {
            // перезапускаем таймер при вводе
            _encodeTimer.Stop();
            _encodeTimer.Start();
        }
        void InitDictionary() {
            // инициализируем словарь основываясь на шаблоне
            _dictionary = HfmDictionary.FromMessage(templateInput.Text);
        }
        void Encode() {
            // если ни разу не кодировали или данные словарь устарел, инициализируемм его
            if (_dictionary is null) InitDictionary();

            // наши пары
            var pairs = new List<EncodedCharPair>(_dictionary!.Count);

            // проходимся по каждому символу в сообщении
            foreach (var @char in messageInput.Text) {
                // если введён неожиданный символ, выходим
                if (!_dictionary.TryGetValue(@char, out var info)) return;

                // добавляем пару
                pairs.Add(new() {
                    Char = @char,
                    Bits = info.Bits,
                    BitCount = info.BitCount
                });
            }

            // устанавливаем в вывод закодированного сообщения наши пары
            encodedMessageOutput.Pairs = pairs;
            // устанавливаем в вывод декодированного сообщения наши пары
            decodedMessageOutput.Pairs = pairs;
            // обновляем дерево
            haffmanTreeOutput.Root = UpdateHaffmanTree(_dictionary.Root);
            // обновляем таблицу частот (не св-во зависимостей, поэтому перерисовываем)
            frequencyTableOutput.Table = _dictionary;
            frequencyTableOutput.InvalidateVisual();
            // декодируем сообщение, и обновляем UI, зависящий от декодированного сообщения
            Decode();
        }
        void Decode() {
            // очищаем textbox'ы
            encodedMessageView.Document.Blocks.Clear();
            decodedMessageView.Document.Blocks.Clear();

            // если сообщение пустое, ничего не кодируем и не декодируем
            if (messageInput.Text.Length == 0) return;

            // кодируем
            var encoded = _dictionary!.Encode(messageInput.Text);
            // добавляем отформатированное закодированное сообщение в соответствующий textbox
            encodedMessageView.Document.Blocks.Add(new Paragraph(new Run(
                FormatBytes(encoded))));
            // тоже самое с декодингом
            var decoded = _dictionary.Decode(encoded);
            decodedMessageView.Document.Blocks.Add(new Paragraph(new Run(decoded[..messageInput.Text.Length])));
            // назодим длину закодированного сообщения (кол-во байтов на 8, чтобы преобразовать в биты)
            var encodedBitCount = encoded.Length * 8;
            // находим минимальное кол-во битов на 1 символ сообщения без сжатия, и умножаем на длину сообщения
            var decodedBitCount = (int)Ceiling(Log2(_dictionary.Count)) * messageInput.Text.Length;
            // выводим в соответствующие textbox'ы длины сообщений и коэффициент сжатия
            coefficientOutput.Document.Blocks.Clear();
            coefficientOutput.Document.Blocks.Add(new Paragraph(new Run(
                $"Коэффициент: {(double)decodedBitCount / encodedBitCount:F2}")));
            coefficientOutput.Document.Blocks.Add(new Paragraph(new Run(
                $"Размер закодированного сообщения: {encodedBitCount}")));
            coefficientOutput.Document.Blocks.Add(new Paragraph(new Run(
                $"Размер декодированного сообщения: {decodedBitCount}")));
        }
        string FormatBytes(ReadOnlySpan<byte> bytes) {
            var builder = new StringBuilder();

            foreach (var @byte in bytes) {
                // от самого правого бита в самому левому
                for (var i = 7; i >= 0; i--) {
                    // добавляем бит
                    builder.Append(((@byte >> i) & 0x1) == 0 ? '0' : '1');

                    // если дошли до середины, то в добавок разделям пробелом
                    if (i == 4) builder.Append(' ');
                }

                // байты тоже разделяем пробелом
                builder.Append(' ');
            }

            return builder.ToString();
        }
        BinaryTreeNode UpdateHaffmanTree(HfmDictionaryNodeBase node) {
            // набор рисунков, объеденённый в 1
            var drawing = new DrawingGroup();
            // рисуем эллипс - непосредственно узел
            drawing.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                null,
                new EllipseGeometry(new Point(0, 0), 20, 20)));
            // - создаём рисунок, а в нём выводим содержимое узла
            using (var innerDrawingContext = drawing.Append()) {
                var formattedInner = FormatString(node is HfmDictionaryLeaf leaf
                    ? leaf.Char.ToString() // если лепесток - то символ
                    : node.Frequency.ToString(), // иначе частоту
                    node is HfmDictionaryLeaf
                        ? FormattedTextType.InnerChar
                        : FormattedTextType.InnerFrequency);
                // центрируем текст
                innerDrawingContext.DrawText(formattedInner, new Point(
                    -formattedInner.Width / 2,
                    -formattedInner.Height / 2));
            }

            // т.к. мы выводим символ в лепестке, то мы теряем информацию о частоте, поэтому
            // выводим её снизу под лепестком
            if (node is HfmDictionaryLeaf) {
                using (var frequencyDrawingContext = drawing.Append()) {
                    var formattedFrequency = FormatString(node.Frequency.ToString(), FormattedTextType.OuterFrequency);
                    frequencyDrawingContext.DrawText(formattedFrequency, new Point(
                        -formattedFrequency.Width / 2, // центрируем
                        25)); // произвольное смещение
                }

                // если лепесток, выходим - больше рисовать нечего
                return new() {
                    Header = drawing
                };
            }

            // если не лепесток
            var _node = (HfmDictionaryNode)node;

            // то проходимся в добавок по подузлам (из-за особенностей дерева оба узла не могут быть null)
            return new() {
                Header = drawing,
                Left = UpdateHaffmanTree(_node.Left),
                Right = UpdateHaffmanTree(_node.Right)
            };
        }
        // форматируем текс тв зависимости от type
        FormattedText FormatString(string text, FormattedTextType type = 0) {
             return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    haffmanTreeOutput.FontFamily,
                    haffmanTreeOutput.FontStyle,
                    type == FormattedTextType.InnerChar ? FontWeights.Bold : FontWeights.Normal,
                    haffmanTreeOutput.FontStretch),
                type == FormattedTextType.OuterFrequency
                    ? 0.8 * haffmanTreeOutput.FontSize
                    : haffmanTreeOutput.FontSize,
                type == FormattedTextType.OuterFrequency ? Brushes.Gray : Brushes.White,
                new NumberSubstitution(),
                1);
        }

        // форматируем текст в зависимости от желаемого вывода
        enum FormattedTextType {
            InnerFrequency, // частота, что выводится внутри узла
            InnerChar, // символ, что выводится внутри лепестка
            OuterFrequency // частота, что рисуется под лепестком
        }
    }
}
