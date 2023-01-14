using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Haffman {
    using static HfmDictionaryNode; // чтобы удобно использовать внутрение классы
    using static Math; // чтобы удобно использовать Min/Max
                       // чтобы отладочная информация содержала не имя типа, а количество элементов
                       // сам словарь содержив в себе 2 части:
                       // часть для кодировщика - реализуемый IReadOnlyDictionary<char, HfmCharInfo> (словарь)
                       // часть для декодера - св-во Root (дерево)
                       
    [DebuggerDisplay("Count = {Count}")]//требовалось для проверки в режиме отладки, проверял содержатя ли нужные символы в свловаре
    public class HfmDictionary : IReadOnlyDictionary<char, HfmCharInfo> {
        readonly IReadOnlyDictionary<char, HfmCharInfo> _values;
        public int Count => _values.Count;
        public HfmCharInfo this[char @char] => _values[@char];
        public IEnumerable<char> Keys => _values.Keys;
        public IEnumerable<HfmCharInfo> Values => _values.Values;
        public Node Root { get; }
        // частота - количество вхождений символов
        // т.к. корень дерева покрывает все символы, то частота корня -
        // кол-во символов в исходном соообщении
        public int CharCount => Root.Frequency;
        public int ByteCount { get; }

        HfmDictionary(IReadOnlyDictionary<char, HfmCharInfo> values, Node root, int byteCount) {
            _values = values;
            Root = root;//объявляю все что нужно
            ByteCount = byteCount;
        }

        // т.к. нам не важен порядок символов в сообщении, а лишь кол-во их вхождений, то
        // мы можем просто использовать таблицу частот
        public static HfmDictionary FromFrequencyTable(IReadOnlyDictionary<char, int> frequencyTable) {
            // превращаем таблицу в список листьев (leaf = лист), а затем из них мы будем собирать дерево вплоть до корня
            var nodes = frequencyTable
                        .Select(pair => new Leaf(pair.Key, pair.Value))//выбираем наш
                        .ToList<HfmDictionaryNode>();//
            // сортировка по убыванию по частотам
            var frequencyComparer = Comparer<HfmDictionaryNode>.Create((left, right) =>// стрелка ибо лямбда(анонимный метод)
                right.Frequency.CompareTo(left.Frequency));//выраженик сравниваем правый с левым 

            // после цикла останется только 1 узел - корень
            while (nodes.Count > 1) {
                // да, каждый раз сортировать медленно, но нормальную сортировку реализовать не получилосб
                nodes.Sort(frequencyComparer);//сортируем с помощью компоратора написанного выше
                // берём 2 узла с наименьшими частотами
                // порядок не важен
                var right = nodes[^1];//так как остосртировали берем 1 бит с конца
                var left = nodes[^2];
                // удаляем их
                nodes.RemoveRange(nodes.Count - 2, 2);//удаляем их из диапазона
                // создаём общего предка, частоты складываем
                var node = new Node(right.Frequency + left.Frequency) {
                    Left = left,
                    Right = right
                };
                // добавляем узел
                nodes.Add(node);
            }

            // после цикла корень может быть как Leaf (если таблица содержала всего 1 символ), так и Node
            var root = nodes[0] is Leaf// проверка лист ли
                // левый узел кодируется 0, присваеваем ему действительный символ
                // в правый узел устанавливаем неиспользуемый символ
                ? new Node(nodes[0].Frequency) {
                    Left = nodes[0],
                    Right = new Leaf('\0', 1)
                } : (Node)nodes[0];
            // теперь составляем непосредственно словарь
            var nodeStack = new Stack<NodeStackEntry>();
            // начинаем составление с корня
            nodeStack.Push(new NodeStackEntry {//заталкиваем корень в словарь 
                Node = root,
                Bits = 0,
                BitCount = 0
            });
            var charInfos = new Dictionary<char, HfmCharInfo>(nodes.Count);
            // общая длина сообщения в битах
            // удобно считать при создании словаря, поэтому можно результат просто кешировать
            // (в св-во ByteCount)
            var bitCount = 0;

            while (nodeStack.Count > 0) {
                // извлекаем узел
                var entry = nodeStack.Pop();

                // если есть подузлы - добавляем в стек
                if (entry.Node is Node node) {
                    // левый узел кодируем как
                    // xxx0, где xxx - код предка
                    nodeStack.Push(new NodeStackEntry {
                        Node = node.Left,
                        Bits = (entry.Bits << 1) | 0,
                        // т.к. мы добавили в начало 1 бит, количество надо также сдвинуть
                        BitCount = entry.BitCount + 1
                    });
                    // правый узел кодируем как
                    // xxx1, где xxx - код предка
                    nodeStack.Push(new NodeStackEntry {
                        Node = node.Right,
                        Bits = (entry.Bits << 1) | 1,
                        BitCount = entry.BitCount + 1
                    });
                    // если же лист, добавляем запись в словарь
                } else if (entry.Node is Leaf leaf) {
                    charInfos.Add(leaf.Char, new HfmCharInfo(entry.Bits, entry.BitCount, leaf.Frequency));
                    // умножая количество бит на 1 вхождение на количество вхождений,
                    // мы получаем кол-во бит, занятых под все вхождения символа
                    // т.к. мы в итоге пройдёмся по всем листьям, т.е. по всем символам
                    // то сумма всех таких произведений даст размер всего сообщения в битах
                    bitCount += entry.BitCount * leaf.Frequency;
                }
            }

            return new HfmDictionary(new ReadOnlyDictionary<char, HfmCharInfo>(charInfos), root,
                // если делится на 8, то просто делим на 8, иначе округляем в большую сторону
                (bitCount & 0x7) == 0 ? bitCount >> 3 : (bitCount >> 3) + 1);
        }
        public static HfmDictionary FromMessage(ReadOnlySpan<char> message) {
            var frequencyTable = new Dictionary<char, int>();

            foreach (var @char in message)
                frequencyTable[@char] = frequencyTable.TryGetValue(@char, out var frequency)
                    ? frequency + 1
                    : 1;

            return FromFrequencyTable(frequencyTable);
        }
        public static byte[] Encode(ReadOnlySpan<char> message, out HfmDictionary dictionary) {
            dictionary = FromMessage(message);

            return dictionary.Encode(message);
        }
        public bool ContainsKey(char @char) => _values.ContainsKey(@char);
        public bool TryGetValue(char @char, [MaybeNullWhen(false)] out HfmCharInfo info) =>
            _values.TryGetValue(@char, out info);
        public IEnumerator<KeyValuePair<char, HfmCharInfo>> GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Encode(Span<byte> output, ReadOnlySpan<char> input) {
            // заглушка для исключительных случаев, крашащих алгоритм
            if (output.IsEmpty || input.IsEmpty)
                return 0;

            // буфер. нам нужны только его первые 8 бит, поэтому тип можно использовать любой
            // использование byte только усложняет выражене buffer = (buffer << ...
            uint buffer = 0;
            var bufferLength = 0;
            // количество закодированных символов
            var encoded = 0;

            foreach (var @char in input) {
                // если символа нет в таблице, выбрасываем ошибку
                if (!TryGetValue(@char, out var info))
                    throw new Exception("Unexpected char.");

                var bits = (ulong)info.Bits;
                var bitCount = info.BitCount;

                // т.к. на 1 символ может приходится больше 8 бит, пушим их через внутренний цикл
                while (true) {
                    // максимум мы можем добавить кол-во бит, равное пустому месту в буфере. поэтому не простое
                    // var bitsToAdd = bitCount
                    var bitsToAdd = Min(8 - bufferLength, bitCount);
                    // 2 части: сдвигаем буфер, освобождая место, и выделяем непосредственно биты для добавления
                    // т.к. мы добавляем в начало, то сдвигаем буфер влево (сдвиг на 2, например):
                    // 0000 0xxx -> 000x xx00
                    //                     ^^
                    //                     освободившееся место
                    // выделение битов также делится на 2 части: сдвиг (убираем всё, что справа) и выделение (убираем всё, что слева)
                    // ...xxxZZZyyy...
                    // ...xxx - всё, что слева от необходимых нам битов
                    // ZZZ - необходимые нам биты
                    // yyy... - всё, что справа
                    // сдвиг влево делает след. преобразование:
                    // ...xxxzzzyyy... -> ...xxxZZZ
                    //       ^
                    //       bitCount
                    // несмотря на то, что есть мусор после (слева) от необходимых битов, bitCount указывает
                    // на них, т.к. мы считываем с конца числа, и вслед за считыванием мы уменьшаем bitCount на кол-во
                    // считанных бит. это, де-факто, стек, только для битов
                    // но мы не убираем мусор, поэтому есть 2-я фаза выделения
                    // ...xxxZZZ
                    // 0000 0111 & // 0000 0111 - маска
                    // ---------
                    // 0000 0ZZZ
                    // с помощью маски мы выделяем болько необходимое, убирая мусор
                    // если мы от 1000 отнимем 1, то получим 0111, т.е. отнимая 1 от степени двойки, мы
                    // место, где находилась единица, превращаем в 0, а всё, что слева, в единицы. это самый простой способ (найденный)
                    // заполнить биты единицами
                    // сдвигая 1 на bitsToAdd, мы добавляем справа от этой единицы bitsToAdd нулей, и отняв 1, мы превращаем их в 1
                    // кол-во единиц будет равно bitsToAdd, что удовлетворяет наши требования
                    // объеденяя 3 операции воедино, получается это присвоение
                    // мы используем ИЛИ, т.к.
                    // 00xx x000
                    // 0000 0ZZZ |
                    // ---------
                    // 00xx xZZZ // желаемый результат
                    buffer = (buffer << bitsToAdd) | ((uint)(bits >> (bitCount - bitsToAdd)) & ((1U << bitsToAdd) - 1));
                    // не забываем контролировать изменение длин
                    bufferLength += bitsToAdd;
                    bitCount -= bitsToAdd;

                    // если буфер заполнен полностью, записываем закодированный байт
                    if (bufferLength == 8) {
                        // т.к. мы записываем байт, то мусор автоматически очищается после upcast'а
                        output[encoded++] = (byte)buffer;
                        //                  ^^^^^^
                        //                  upcast
                        // очищаем буфер
                        bufferLength = 0;

                        // если записывать больше некуда, выходим из функции
                        if (encoded == output.Length)
                            return encoded;
                    }
                    // если мы полностью записали символ, то выходим из внутреннего цикла
                    if (bitCount == 0)
                        break;
                }
            }

            // после цикла наш буфер может ещё быть заполнен чем-либо, то т.к. его длина не равна 8
            // (иначе бы буфер записал свои данные в соответствующием if'е, и его длина была бы равна 0),
            // то здесь уже нужно выровнять буфер
            // xxxx xxxx | yyyZ ZZZZ
            //                ^
            //                bufferLength
            // xxx - последний записанный байт
            // yyy - мусор
            // ZZZ - то, что нужно записать
            // т.к. output - это битовый поток, то нам нужно выровнять биты по левой границе, т.е.
            // yyyZ ZZZZ -> ZZZZ Z000
            // это есть ничто иное, как сдвиг влево, т.к. bufferLength - кол-во не мусора, то
            // 8 - bufferLength - количество мусора
            if (bufferLength != 0)
                output[encoded++] = (byte)(buffer << (8 - bufferLength));

            return encoded;
        }
        public byte[] Encode(ReadOnlySpan<char> input) {
            var output = new byte[GetByteCount(input)];
            Encode(output, input);

            return output;
        }
        // версия Encode без кодирования
        // можно использовать вместо ByteCount для кодирования сообщений с таким же набором символов
        // и, желательно, с таблицей частот
        public int GetByteCount(ReadOnlySpan<char> input) {
            if (input.IsEmpty)
                return 0;

            var count = 0;

            foreach (var @char in input) {
                if (!TryGetValue(@char, out var info))
                    throw new Exception("Unexpected char.");

                count += info.BitCount;
            }

            return (count & 0x7) == 0 ? count >> 3 : (count >> 3) + 1;
        }
        public int Decode(Span<char> output, ReadOnlySpan<byte> input) {
            // такая же заглушка, как и в Encode
            if (output.IsEmpty || input.IsEmpty)
                return 0;

            // буфер не пустой, можем оптимищировать и считать 1 байт перед циклом
            byte buffer = input[0];
            var bufferLength = 8;
            // сколько считано из input
            var readed = 1;
            // сколько записано в output
            var decoded = 0;
            // используем дерево
            var node = Root;

            while (decoded < output.Length) {
                // если биты кончились, считываем из input
                if (bufferLength == 0) {
                    // если считывать больше нечего, выходим из цикла
                    if (readed == input.Length)
                        break;

                    buffer = input[readed++];
                    bufferLength = 8;
                }

                // выделяем 1 крайний бит
                // xxxZ ZZZZ - буфер
                // xxxZ ZZZZ >> bufferLength == 0000 0xxx
                // сдвинув на 1 меньше, мы получим
                // 0000 xxxZ // крайний бит
                // 0000 0001 & // маска
                // ---------
                // 0000 000Z // выделили бит
                // если он 1, то идём по правому узлу, иначе по левому
                var next = ((buffer >> (bufferLength - 1)) & 0x1) != 0
                    ? node.Right
                    : node.Left;
                // контролируем длину
                bufferLength--;

                // если мы считали достаточно бит, то мы должны дойти до листа, а значит
                // декодируем символ
                if (next is Leaf leaf) {
                    output[decoded++] = leaf.Char;
                    // не забываем возвращаться обратно
                    node = Root;
                } else
                    node = (Node)next;
            }

            return decoded;
        }
        public string Decode(ReadOnlySpan<byte> input) {
            var output = new char[GetCharCount(input)];
            Decode(output, input);

            return new string(output);
        }
        // см. GetByteCount
        public int GetCharCount(ReadOnlySpan<byte> input) {
            if (input.IsEmpty)
                return 0;

            byte buffer = input[0];
            var bufferLength = 8;
            var readed = 1;
            var decoded = 0;
            var node = Root;

            while (true) {
                if (bufferLength == 0) {
                    if (readed == input.Length)
                        break;

                    buffer = input[readed++];
                    bufferLength = 8;
                }

                var next = ((buffer >> (bufferLength - 1)) & 0x1) != 0
                    ? node.Right
                    : node.Left;
                bufferLength--;

                if (next is Leaf) {
                    decoded++;
                    node = Root;
                } else
                    node = (Node)next;
            }

            return decoded;
        }

        // информация об узле при составлении словаря
        struct NodeStackEntry {
            public HfmDictionaryNode Node;
            public long Bits;
            public int BitCount;
        }
    }
}
