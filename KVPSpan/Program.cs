using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SRCS = System.Runtime.CompilerServices;

namespace KVPSpan
{
    // KeyValue from Spreads.Core

    public enum KeyValuePresence
    {
        BothMissing = 0,
        BothPresent = -1,
        KeyMissing = -2, // E.g. for Fill(x) we do not calculate where the previous key was, just check if it exists and if not return given value.
        ValueMissing = -3,
    }




    // NB `in` in ctor gives 95% of perf gain, not `ref struct`, but keep it as ref struct for semantics and potential upgrade to ref fields when they are implemented
    // Also it's fatter due to version so it's better to restrict saving it in arrays/fields.

    // TODO xml docs
    /// <summary>
    /// Stack-only struct that represents references to a key and value pair. It's like an
    /// Opt[KeyValuePair[TKey, TValue]], but Opt cannot have ref struct.
    /// It has IsMissing/IsPresent properties that must be checked if this struct could be
    /// `undefined`/default/null, but Key and Value properties do not check this condition
    /// for performance reasons.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public ref struct KeyValue<TKey, TValue>
    {
        // NB See https://github.com/dotnet/csharplang/issues/1147
        // and https://github.com/dotnet/corert/blob/796aeaa64ec09da3e05683111c864b529bcc17e8/src/System.Private.CoreLib/src/System/ByReference.cs
        // Try using it when it is made public

        // ReSharper disable InconsistentNaming
        // NB could be used from cursors
        internal TKey _k;
        internal TValue _v;
        // ReSharper restore InconsistentNaming

        public TKey Key
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                if (IsMissing) { throw new NullReferenceException("Must check IsMissing property of KeyValue before accesing Key/Value if unsure that a value exists"); }
#endif
                return _k;
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
                return _v;
            }
        }

        //public bool IsMissing
        //{
        //    [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        //    get
        //    {
        //        return _version == 0;
        //    }
        //}

        //public bool IsPresent
        //{
        //    [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        //    get
        //    {
        //        return !IsMissing;
        //    }
        //}

        //public KeyValuePresence Presence
        //{
        //    [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        //    get
        //    {
        //        if (_version == -1 || _version > 0)
        //        {
        //            return KeyValuePresence.BothPresent;
        //        }
        //        return (KeyValuePresence)_version;
        //    }
        //}

        //public long Version
        //{
        //    [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        //    get
        //    {
        //        return _version;
        //    }
        //}

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(in TKey k, in TValue v)
        {
            _k = k;
            _v = v;
            // _version = -1;
        }

        //[SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        //public KeyValue(in TKey k, in TValue v, in long version)
        //{
        //    //if (version <= 0)
        //    //{
        //    //    ThrowHelper.ThrowArgumentException("Version is zero or negative!");
        //    //}
        //    _k = k;
        //    _v = v;
        //    _version = version;
        //}

        //[SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        //internal KeyValue(in TKey k, in TValue v, in long version, in KeyValuePresence presense)
        //{
        //    // TODO bit flags for presense, int62 shall be enough for everyone
        //    _k = k;
        //    _v = v;
        //    _version = version;
        //}

        // TODO make implicit after refactoring all projetcs
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new KeyValue<TKey, TValue>(kvp.Key, kvp.Value);
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

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<long, double> GetKVP(int i)
        {
            return new KeyValuePair<long, double>(_keys[i], _values[i]);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue<long, double> GetKV(int i)
        {
            return new KeyValue<long, double>(_keys[i], _values[i]);
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
                        var kv = container.GetKV(i);
                        sum2 += kv.Key + kv.Value;
                    }
                }

                sw.Stop();
                if (sum2 < long.MaxValue)
                {
                    Console.WriteLine($"KV elapsed {sw.ElapsedMilliseconds} with sum: {sum2}");
                }

            }

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}