using System;
using System.Collections.Generic;
using System.Diagnostics;
using SRCS = System.Runtime.CompilerServices;

namespace KVPSpan
{
    public readonly unsafe ref struct KeyValue<TKey, TValue>
    {
        // NB See https://github.com/dotnet/csharplang/issues/1147
        // and https://github.com/dotnet/corert/blob/796aeaa64ec09da3e05683111c864b529bcc17e8/src/System.Private.CoreLib/src/System/ByReference.cs
        // Use it when it is made public

        private readonly IntPtr _kp;

        private readonly IntPtr _vp;

        public TKey Key
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                if (IsMissing) { throw new NullReferenceException("Must check IsMissing property of KeyValue before accesing Key/Value if unsure that a value exists"); }
#endif
                // NB On x86_64 no visible perf diff, use Unaligned te be safe than sorry later
                return SRCS.Unsafe.ReadUnaligned<TKey>((void*)_kp);
            }
        }

        public TValue Value
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                if (IsMissing) { throw new NullReferenceException("Must check IsMissing property of KeyValue before accesing Key/Value if unsure that a value exists"); }
#endif
                // NB On x86_64 no visible perf diff, use Unaligned te be safe than sorry later
                return SRCS.Unsafe.ReadUnaligned<TValue>((void*)_vp);
            }
        }

        public bool IsMissing
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return _kp == IntPtr.Zero && _vp == IntPtr.Zero;
            }
        }

        public bool IsPresent
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return !(_kp == IntPtr.Zero && _vp == IntPtr.Zero);
            }
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(in TKey k, in TValue v)
        {
            _kp = (IntPtr)SRCS.Unsafe.AsPointer(ref SRCS.Unsafe.AsRef(k));
            _vp = (IntPtr)SRCS.Unsafe.AsPointer(ref SRCS.Unsafe.AsRef(v));
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(TKey k, TValue v)
        {
            _kp = (IntPtr)SRCS.Unsafe.AsPointer(ref k);
            _vp = (IntPtr)SRCS.Unsafe.AsPointer(ref v);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(KeyValuePair<TKey, TValue> kvp)
        {
            var k = kvp.Key;
            var v = kvp.Value;
            _kp = (IntPtr)SRCS.Unsafe.AsPointer(ref k);
            _vp = (IntPtr)SRCS.Unsafe.AsPointer(ref v);
        }

        // TODO make implicit after refactoring all projetcs
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new KeyValue<TKey, TValue>(kvp);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValuePair<TKey, TValue>(KeyValue<TKey, TValue> kv)
        {
            return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
        }
    }

    unsafe public struct Container
    {
        public int Count { get; }
        private long[] _keys;
        private double[] _values;

        public Container(int count)
        {
            Count = count;
            _keys = new long[Count];
            _values = new double[Count];
            for (int i = 0; i < Count; i++)
            {
                _keys[i] = i;
                _values[i] = i;
            }
        }

        public KeyValuePair<long, double> GetKVP(int i)
        {
            return new KeyValuePair<long, double>(_keys[i], _values[i]);
        }

        public KeyValue<long, double> GetKVS(int i)
        {
            //fixed (void* pk = &_keys[0])
            //fixed (void* pv = &_values[0])
            //{
            //    return new KeyValueSpan<long, double>(Unsafe.Add<long>(pk, i), Unsafe.Add<double>(pv, i));
            //}
            return new KeyValue<long, double>(in _keys[i], in _values[i]);
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var rounds = 10;
            var repeat = 10000;
            var count = 100_000;
            // sum values from KVP and KVS

            var container = new Container(count);
            var sw = new Stopwatch();

            for (int r = 0; r < rounds; r++)
            {
                var sum1 = 0.0;

                sw.Restart();
                for (int j = 0; j < repeat; j++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var kv = container.GetKVP(i);
                        sum1 += kv.Key + kv.Value;
                    }
                }
                sw.Stop();
                if (sum1 < long.MaxValue)
                {
                    Console.WriteLine($"KVP elapsed {sw.ElapsedMilliseconds} with sum: {sum1}");
                }

                var sum2 = 0.0;
                sw.Restart();
                for (int j = 0; j < repeat; j++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var kv = container.GetKVS(i);
                        sum2 += kv.Key + kv.Value;
                    }
                }

                sw.Stop();
                if (sum2 < long.MaxValue)
                {
                    Console.WriteLine($"KVS elapsed {sw.ElapsedMilliseconds}  with sum: {sum2}");
                }

                //var sum3 = 0.0;
                //sw.Restart();
                //for (int j = 0; j < repeat; j++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        var kvp = container.GetKVP(i);
                //        var kv = new KeyValue<long, double>(kvp);
                //        sum3 += kv.Key + kv.Value;
                //    }
                //}

                //sw.Stop();
                //if (sum2 < long.MaxValue)
                //{
                //    Console.WriteLine($"KVP -> KVS elapsed {sw.ElapsedMilliseconds}  with sum: {sum2}");
                //}
            }

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}