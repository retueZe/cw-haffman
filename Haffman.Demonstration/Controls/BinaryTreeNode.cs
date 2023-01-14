using System;
using System.Windows.Markup;
using System.Windows.Media;

namespace Haffman.Demonstration.Controls {

    [ContentProperty("Header")]
    public sealed class BinaryTreeNode {
        public BinaryTreeNode? Left { get; set; }
        public BinaryTreeNode? Right { get; set; }
        public Drawing? Header { get; set; }
    }
}
