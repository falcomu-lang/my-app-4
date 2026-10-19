using System;
using System.Runtime.InteropServices;

namespace CameraCaptureApp.Native
{
    internal static class Lsi8181Native
    {
        public const uint Success = 0;
        public const byte CardIdMax = 15;
        public const byte QuadratureMode = 0;
        public const byte DebounceTime1Us = 1;
        public const byte Multiple4 = 0;
        public const byte Multiple2 = 1;
        public const byte Multiple1 = 2;
        public const byte CompareAutoIncrement = 2;
        public const byte CounterCompare = 2;
        public const byte CmpOutPulse = 1;
        public const byte CmpOutEnabled = 1;
        public const byte CmpOutNormalPolarity = 0;
        public const byte NoGate = 0;
        public const byte APhasePolarityBit = 0;

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_initial();

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_close();

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_info(byte CardID, ref ulong IO_address, ref ulong TC_address);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_CI_mode_set(byte CardID, byte in_mode, byte debounce_time, byte multiple_rate);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_CO_mode_set(byte CardID, byte out_mode, byte gate, ushort out_width);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_CMP_OUT_set(byte CardID, byte polarity, byte out_mode, ushort out_width);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_counter_set(byte CardID, int counter_value);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_counter_read(byte CardID, ref int counter_value);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_value_set(byte CardID, int compare_value);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_value_read(byte CardID, ref int compare_value);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_increment_set(byte CardID, int increment_value);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_mode_set(byte CardID, byte compare_mode);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_counter_start(byte CardID, byte mode);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_counter_stop(byte CardID);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_toggle_preset(byte CardID, byte preset);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_CIO_polarity_set(byte CardID, ushort polarity);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_CIO_polarity_read(byte CardID, ref ushort polarity);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_set(byte CardID, byte channel, short offset);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_read(byte CardID, byte channel, ref short offset);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_out_width_set(byte CardID, byte channel, ushort out_width);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_out_width_read(byte CardID, byte channel, ref ushort out_width);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_mask_set(byte CardID, byte mask);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_mask_read(byte CardID, ref byte mask);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_output_point_set(byte CardID, byte point, byte state);

        [DllImport("LSI8181_64.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern uint LSI8181_compare_offset_output_point_read(byte CardID, byte point, ref byte state);

    }
}
