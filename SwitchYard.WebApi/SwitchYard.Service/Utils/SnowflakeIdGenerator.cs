namespace SwitchYard.Service.Utils
{
    /// <summary>
    /// 雪花算法ID生成器
    /// Twitter的分布式自增ID算法（Snowflake）
    /// </summary>
    public class SnowflakeIdGenerator
    {
        // 开始时间戳 (2024-01-01 00:00:00)
        private const long Twepoch = 1704067200000L;

        // 机器ID所占的位数
        private const int WorkerIdBits = 5;

        // 数据标识ID所占的位数
        private const int DatacenterIdBits = 5;

        // 序列在ID中占的位数
        private const int SequenceBits = 12;

        // 机器ID向左移12位
        private const int WorkerIdShift = SequenceBits;

        // 数据标识ID向左移17位(12+5)
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;

        // 时间戳向左移22位(5+5+12)
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

        // 生成序列的掩码，这里为4095 (0b111111111111=0xfff=4095)
        private const long SequenceMask = -1L ^ (-1L << SequenceBits);

        // 支持的最大机器ID，结果是31 (这个移位算法可以很快的计算出几位二进制数所能表示的最大十进制数)
        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);

        // 支持的最大数据标识ID，结果是31
        private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);

        private readonly object _lock = new object();
        private long _sequence = 0L;
        private long _lastTimestamp = -1L;

        /// <summary>
        /// 工作机器ID(0~31)
        /// </summary>
        public long WorkerId { get; protected set; }

        /// <summary>
        /// 数据中心ID(0~31)
        /// </summary>
        public long DatacenterId { get; protected set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workerId">工作机器ID</param>
        /// <param name="datacenterId">数据中心ID</param>
        public SnowflakeIdGenerator(long workerId = 0, long datacenterId = 0)
        {
            if (workerId > MaxWorkerId || workerId < 0)
            {
                throw new ArgumentException($"worker Id can't be greater than {MaxWorkerId} or less than 0");
            }

            if (datacenterId > MaxDatacenterId || datacenterId < 0)
            {
                throw new ArgumentException($"datacenter Id can't be greater than {MaxDatacenterId} or less than 0");
            }

            WorkerId = workerId;
            DatacenterId = datacenterId;
        }

        /// <summary>
        /// 生成下一个ID（线程安全）
        /// </summary>
        /// <returns>唯一ID</returns>
        public long NextId()
        {
            lock (_lock)
            {
                var timestamp = GetCurrentTimestamp();

                // 如果当前时间小于上一次ID生成的时间戳，说明系统时钟回退过，这个时候应当抛出异常
                if (timestamp < _lastTimestamp)
                {
                    throw new InvalidOperationException(
                           $"Clock moved backwards. Refusing to generate id for {_lastTimestamp - timestamp} milliseconds");
                }

                // 如果是同一时间生成的，则进行毫秒内序列
                if (_lastTimestamp == timestamp)
                {
                    _sequence = (_sequence + 1) & SequenceMask;
                    // 毫秒内序列溢出
                    if (_sequence == 0)
                    {
                        // 阻塞到下一个毫秒，获得新的时间戳
                        timestamp = WaitNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    // 时间戳改变，毫秒内序列重置
                    _sequence = 0L;
                }

                // 上次生成ID的时间戳
                _lastTimestamp = timestamp;

                // 移位并通过或运算拼到一起组成64位的ID
                return ((timestamp - Twepoch) << TimestampLeftShift)
            | (DatacenterId << DatacenterIdShift)
                       | (WorkerId << WorkerIdShift)
               | _sequence;
            }
        }

        /// <summary>
        /// 生成字符串类型的ID
        /// </summary>
        /// <returns>唯一ID字符串</returns>
        public string NextIdString()
        {
            return NextId().ToString();
        }

        /// <summary>
        /// 阻塞到下一个毫秒，直到获得新的时间戳
        /// </summary>
        /// <param name="lastTimestamp">上次生成ID的时间戳</param>
        /// <returns>当前时间戳</returns>
        protected long WaitNextMillis(long lastTimestamp)
        {
            var timestamp = GetCurrentTimestamp();
            while (timestamp <= lastTimestamp)
            {
                timestamp = GetCurrentTimestamp();
            }
            return timestamp;
        }

        /// <summary>
        /// 获取当前时间戳（毫秒）
        /// </summary>
        /// <returns>当前时间戳</returns>
        protected long GetCurrentTimestamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
