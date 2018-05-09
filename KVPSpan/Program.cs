using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace KVPSpan
{
    public unsafe struct KeyValueRef<TKey, TValue>
    {
        private void* kp;
        private void* vp;

        public TKey Key
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.ReadUnaligned<TKey>(kp);
            }
        }

        public TValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.ReadUnaligned<TValue>(vp);
            }
        }

        public KeyValueRef(ref TKey k, ref TValue v)
        {
            kp = Unsafe.AsPointer(ref k);
            vp = Unsafe.AsPointer(ref v);
        }

        public KeyValueRef(TKey k, TValue v)
        {
            kp = Unsafe.AsPointer(ref k);
            vp = Unsafe.AsPointer(ref v);
        }

        public KeyValueRef(KeyValuePair<TKey, TValue> kvp)
        {
            var k = kvp.Key;
            var v = kvp.Value;
            kp = Unsafe.AsPointer(ref k);
            vp = Unsafe.AsPointer(ref v);
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

        public KeyValueRef<long, double> GetKVS(int i)
        {
            //fixed (void* pk = &_keys[0])
            //fixed (void* pv = &_values[0])
            //{
            //    return new KeyValueSpan<long, double>(Unsafe.Add<long>(pk, i), Unsafe.Add<double>(pv, i));
            //}
            return new KeyValueRef<long, double>(ref _keys[i], ref _values[i]);
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
                var sum2 = 0.0;
                var sum3 = 0.0;

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

                sw.Restart();
                for (int j = 0; j < repeat; j++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var kvp = container.GetKVP(i);
                        var kv = new KeyValueRef<long, double>(kvp);
                        sum3 += kv.Key + kv.Value;
                    }
                }

                sw.Stop();
                if (sum2 < long.MaxValue)
                {
                    Console.WriteLine($"KVP -> KVS elapsed {sw.ElapsedMilliseconds}  with sum: {sum2}");
                }
            }

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}