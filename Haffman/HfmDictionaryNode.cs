using System;
//узлы словаря 
namespace Haffman {
    // узел дерева
    public record HfmDictionaryNode {
        // суммарная частота всех символов всех подузлов, что содержит данный узел
        public int Frequency { get; }

        HfmDictionaryNode(int frequency) {
            if (frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be positive.");

            Frequency = frequency;
        }

        // узел, имеющий подузлы
        // т.к. ему не нужно хранить символ, выделен в отдельный класс
        public record Node : HfmDictionaryNode {
            // init нужен для подобной инициализации:
            // new Node(123) {
            //     Left = ...,
            //     Right = ...
            // };
            // при этом св-ва в дальнейшем считывать не получится
            public HfmDictionaryNode Left { get; init; }
            public HfmDictionaryNode Right { get; init; }

            public Node(int frequency) : base(frequency) { }//двоеточие чтобы указать параметры
        }
        // конечный узел (не имеет подузлов)
        public record Leaf : HfmDictionaryNode {
            public char Char { get; }

            public Leaf(char @char, int frequency) : base(frequency) {//собака нужна ибо char зарезервированное слово
                Char = @char;
            }
        }
    }
}
