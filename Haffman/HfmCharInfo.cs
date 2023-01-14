namespace Haffman {
    // Bits, BitCount - биты, которыми кодируется символ
    // Frequency - сколько раз символ встречался в сообщении
    public record HfmCharInfo(long Bits, int BitCount, int Frequency) { }
}
