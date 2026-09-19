using System;
using UnityEngine;

namespace GameSDK
{
    /// <summary>
    /// Unix 时间戳的计数单位。
    /// </summary>
    public enum TimestampUnit
    {
        /// <summary>秒。</summary>
        Seconds,
        /// <summary>毫秒。</summary>
        Milliseconds
    }

    /// <summary>
    /// 提供 Unix 时间戳、服务器时间同步和日期解析。
    /// </summary>
    public static class UtilsTime
    {
        private const int MillisecondsPerSecond = 1000;
        // 运行秒数保留 9 位小数，为完整 long 整数预留足够的 decimal 精度。
        private const int RealtimeDecimalPlaces = 9;

        // 服务器秒时间戳 - Unity 本地运行秒数；decimal 同时保留 long 秒和偏差的小数部分。
        private static decimal _serverTimeOffsetSeconds;
        private static bool _hasServerTimeSync;

        /// <summary>
        /// 同步服务器的秒时间戳；重复同步会替换之前的时间基准。
        /// </summary>
        /// <param name="second">服务器当前 Unix 秒时间戳。</param>
        public static void SyncServerTime(long second)
        {
            decimal realtimeSeconds = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            _serverTimeOffsetSeconds = second - realtimeSeconds;
            _hasServerTimeSync = true;
        }

        /// <summary>
        /// 获取服务器秒时间戳；自同步起每经过一整秒才推进一秒。
        /// </summary>
        /// <returns>推算的服务器 Unix 秒时间戳。</returns>
        /// <exception cref="InvalidOperationException">尚未同步服务器时间。</exception>
        /// <exception cref="OverflowException">结果超出 long 范围。</exception>
        public static long TimeServerSecond()
        {
            if (!_hasServerTimeSync)
            {
                throw new InvalidOperationException("尚未同步服务器时间。");
            }

            decimal serverSeconds = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces)
                + _serverTimeOffsetSeconds;
            return checked((long)decimal.Floor(serverSeconds));
        }

        /// <summary>
        /// 获取服务器毫秒时间戳，由同步的整秒基准和本地经过时间推算。
        /// 同步时毫秒部分为零，不代表服务器提供了毫秒精度。
        /// </summary>
        /// <returns>推算的服务器 Unix 毫秒时间戳。</returns>
        /// <exception cref="InvalidOperationException">尚未同步服务器时间。</exception>
        /// <exception cref="OverflowException">结果超出 long 范围。</exception>
        public static long TimeServerMillsecond()
        {
            if (!_hasServerTimeSync)
            {
                throw new InvalidOperationException("尚未同步服务器时间。");
            }

            decimal serverSeconds = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces)
                + _serverTimeOffsetSeconds;
            return checked((long)decimal.Floor(serverSeconds * MillisecondsPerSecond));
        }

        /// <summary>
        /// 获取本机系统时钟的当前 Unix 秒时间戳。
        /// </summary>
        /// <returns>当前 Unix 秒时间戳。</returns>
        public static long TimeLocalSecond()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 获取本机系统时钟的当前 Unix 毫秒时间戳。
        /// </summary>
        /// <returns>当前 Unix 毫秒时间戳。</returns>
        public static long TimeLocalMillsecond()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 按当前文化解析日期时间字符串并转换为 Unix 时间戳。
        /// 未包含时区的字符串按本地时区解释。
        /// </summary>
        /// <param name="dateTimeString">日期时间字符串，例如 "2025-5-7 20:00:00"。</param>
        /// <param name="unit">时间戳单位：秒或毫秒。</param>
        /// <param name="timestamp">成功时为转换结果，可为零或负数；解析失败时为零。</param>
        /// <returns>解析成功返回 true；null、空白或格式错误返回 false。</returns>
        /// <exception cref="ArgumentOutOfRangeException">unit 无效；在解析日期之前检查。</exception>
        public static bool TryConvertToTimestamp(string dateTimeString, TimestampUnit unit, out long timestamp)
        {
            if (unit != TimestampUnit.Seconds && unit != TimestampUnit.Milliseconds)
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "无效的时间戳单位。");

            timestamp = 0;
            if (!DateTime.TryParse(dateTimeString, out DateTime dateTime))
                return false;

            var utcDateTime = new DateTimeOffset(dateTime.ToUniversalTime());
            if (unit == TimestampUnit.Seconds)
            {
                timestamp = utcDateTime.ToUnixTimeSeconds();
            }
            else
            {
                timestamp = utcDateTime.ToUnixTimeMilliseconds();
            }
            return true;
        }
    }
}
