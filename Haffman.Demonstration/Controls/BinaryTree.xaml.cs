using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Haffman.Demonstration.Controls {
    using static Math;
    [ContentProperty("Root")]
    public partial class BinaryTree : UserControl {
        public static readonly DependencyProperty RootProperty;
        public static readonly DependencyProperty ConnectionPenProperty;

        // корневой узел дерева
        public BinaryTreeNode? Root {
            get => GetValue(RootProperty) as BinaryTreeNode;
            set => SetValue(RootProperty, value);
        }
        // перо для отрисовки веток
        public Pen? ConnectionPen {
            get => GetValue(ConnectionPenProperty) as Pen;
            set => SetValue(ConnectionPenProperty, value);
        }

        static BinaryTree() {
            RootProperty = DependencyProperty.Register(
                "Root",
                typeof(BinaryTreeNode),
                typeof(BinaryTree),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsRender));
            ConnectionPenProperty = DependencyProperty.Register(
                "Pen",
                typeof(Pen),
                typeof(BinaryTree),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsRender));
        }
        public BinaryTree() {
            InitializeComponent();
        }

        protected override void OnRender(DrawingContext context) {
            // задник
            if (Background is not null)
                context.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
            // если нет корня - выходим
            if (Root is null) return;

            // считаме 
            var depth = ComputeTreeBounds(out var bounds);

            // если глубина равна 0,
            if (depth == 0) return;

            DrawNode(context, Root, new Rect(bounds.X, bounds.Y, bounds.Width, depth == 1
                ? bounds.Height // избегаем деления на 0
                : bounds.Height / (depth - 1)));
        }
        void DrawNode(DrawingContext context, BinaryTreeNode node, Rect headerBounds) {
            // headerBounds - границы для отрисовки самого узла и его веток
            // если перо для рисования веток не указано, не рисуем их
            if (ConnectionPen is not null) {
                var origin = new Point(
                    headerBounds.X + headerBounds.Width / 2, // по центру узла
                    headerBounds.Y + node.Header?.Bounds.Width ?? 0); // прямо под ним

                // деля область отрисовки под узлом на 2 части и выполняя отрисовку через DrawNode
                // избегаются любые столкновения
                if (node.Left is not null) DrawConnection(context, origin, new Point(
                    headerBounds.X + headerBounds.Width / 4, // если разленить половину на пополам, получится 1/4
                    headerBounds.Y + headerBounds.Height));
                if (node.Right is not null) DrawConnection(context, origin, new Point(
                    headerBounds.X + headerBounds.Width * 3 / 4, // таже 1/4, только с другой стороны
                    headerBounds.Y + headerBounds.Height));
            }
            // если нужно отрисовать сам узел
            if (node.Header is not null) {
                // создаём кисть, основаную на рисунке узла
                var brush = new DrawingBrush(node.Header);
                // создаём клип для узла
                var headerClip = new Rect(
                    headerBounds.X + (headerBounds.Width - node.Header.Bounds.Width) / 2, // центрируем
                    headerBounds.Y,
                    // указываем размеры рисунка
                    node.Header.Bounds.Width,
                    node.Header.Bounds.Height);
                context.DrawRectangle(brush, null, headerClip);
            }
            // если есть левый узел
            if (node.Left is not null) DrawNode(context, node.Left, new Rect(
                headerBounds.X,
                headerBounds.Y + headerBounds.Height,
                headerBounds.Width / 2,
                headerBounds.Height));
            // если есть правый узел
            if (node.Right is not null)
                DrawNode(context, node.Right, new Rect(
                headerBounds.X + headerBounds.Width / 2,
                headerBounds.Y + headerBounds.Height,
                headerBounds.Width / 2,
                headerBounds.Height));
        }
        void DrawConnection(DrawingContext context, Point start, Point end) {
            // для красивой линии используем кривую безье, основанную на 4-х точках
            // т.к. нам нажно, чтобы соединение выходило и входило под прямым углом, а также
            // проходило через середину прямой м-ду start и end, то эта конфигурация -
            // единствунный возможный out
            var point1 = new Point(start.X, (end.Y + start.Y) / 2);
            var point2 = new Point(end.X, (end.Y + start.Y) / 2);
            // создаём геометрию пути для нашей кривой
            var geometry = new PathGeometry(new[] {
                new PathFigure(start, new[] {
                    new BezierSegment(point1, point2, end, true)
                }, false)
            });
            // рисуем кривую
            context.DrawGeometry(null, ConnectionPen, geometry);
            // если идём вправо (end.X > start.X), то 1, иначе 0
            var bit = end.X > start.X ? "1" : "0";
            var formattedBit = FormatString(bit);
            // рисуем бит
            context.DrawText(formattedBit, new Point(
                (start.X + end.X - formattedBit.Width) / 2,
                point1.Y - formattedBit.Height - 5)); // 5 - произвольное смещение
        }
        FormattedText FormatString(string text) {
            return new FormattedText(
               text,
               CultureInfo.CurrentCulture,
               FlowDirection.LeftToRight,
               new Typeface(
                   FontFamily,
                   FontStyle,
                   FontWeight,
                   FontStretch),
               0.6 * FontSize,
               Brushes.Gray,
               new NumberSubstitution(),
               1);
        }
        int ComputeTreeBounds(out Rect bounds) {
            bounds = new Rect();

            if (Root is null) return 0;

            var left = Root;
            var right = Root;

            // находим самый крайний левый и правый узлы
            while (left.Left is not null) left = left.Left;
            while (right.Right is not null) right = right.Right;

            // находим, какой из них дальше от центра
            // берём ширину его рисунка, и считаем это за смещение
            var offset = Max(left.Header?.Bounds.Width ?? 0, right.Header?.Bounds.Width ?? 0);
            // раскомментируй строчку, чтобы посмотреть эффект
            // offset = 0;
            // находим глубину дерева, а так же высоту самого высокого рисунка узла, находящегося на последнем слою
            var (maxDepth, maxHeight) = ComputeMaxDepth(Root, 1, 0);
            // тупо формула, которую я сам вывел (см. фото в вк)
            var p = Pow(2, maxDepth - 1);
            var newWidth = (ActualWidth - offset) * (p / (p - 1));
            newWidth = Max(newWidth, ActualWidth);
            maxHeight = Min(maxHeight, ActualHeight);
            // раскомментируй строчки, чтобы посмотреть эффект
            // newWidth = ActualWidth;
            // maxHeight = 0;
            // (ActualWidth - newWidth) / 2 - центрирование
            bounds = new Rect((ActualWidth - newWidth) / 2, 0, newWidth, ActualHeight - maxHeight);

            return maxDepth;
        }
        // гугли "c# tuples"
        (int MaxDepth, double MaxHeight) ComputeMaxDepth(BinaryTreeNode node, int depth, double height) {
            // рекурсивно считаем для правого и левого подузлов
            var (leftMaxDepth, leftMaxHeight) = node.Left is null
                ? (0, 0)
                : ComputeMaxDepth(node.Left, depth, height);
            var (rightMaxDepth, rightMaxHeight) = node.Right is null
                ? (0, 0)
                : ComputeMaxDepth(node.Right, depth, height);
            // находим максимальную глубину
            var maxDepth = depth + Max(leftMaxDepth, rightMaxDepth);
            // если это лепесток (максимальная глубина равна нашей глубине), то
            // максимальной высотой будет высота рисунка этого лепестка
            var maxHeight = maxDepth == depth
                ? node.Header?.Bounds.Height ?? 0
                : leftMaxDepth > rightMaxDepth // иначе возвращаем максимальную высоту в подузлах
                    ? leftMaxHeight
                    : leftMaxDepth < rightMaxDepth
                        ? rightMaxDepth
                        : Max(leftMaxHeight, rightMaxHeight);

            return (maxDepth, maxHeight);
        }
    }
}
