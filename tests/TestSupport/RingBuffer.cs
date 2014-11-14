using System;
using System.Threading;

using NUnit.Framework;


// ReSharper disable once CheckNamespace
namespace bz2portable.Tests.TestSupport
{
    /// <summary>
    /// A fixed size buffer of bytes.  Both reading and writing are supported.
    /// Reading from an empty buffer will wait until data is written.  Writing to a full buffer
    /// will wait until data is read.
    /// </summary>
    public class ReadWriteRingBuffer
    {
        #region Constructors
        /// <summary>
        /// Create a new RingBuffer with a specified size.
        /// </summary>
        /// <param name="size">The size of the ring buffer to create.</param>
        public ReadWriteRingBuffer(int size)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("size");
            }

            array_ = new byte[size];
            lockObject_ = new object();

			notEmptyEvent_ = new ManualResetEvent(false);
			notFullEvent_ = new ManualResetEvent(true);
        }
        #endregion

        /// <summary>
        /// Clear the buffer contents.
        /// </summary>
        public void Clear()
        {
            tail_ = 0;
            head_ = 0;
            count_ = 0;

            Array.Clear(array_, 0, array_.Length);

			notFullEvent_.Set();
			notEmptyEvent_.Reset();
        }

        /// <summary>
        /// Close the buffer for writing.
        /// </summary>
        /// <remarks>A Read when the buffer is closed and there is no data will return -1.</remarks>
        public void Close()
        {
            isClosed_ = true;
			notEmptyEvent_.Set();
        }

        /// <summary>
        /// Write adds a byte to the head of the RingBuffer.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void WriteByte(byte value)
        {
            if (isClosed_)
            {
                throw new ApplicationException("Buffer is closed");
            }

			notFullEvent_.WaitOne();

            lock (lockObject_)
            {
                array_[head_] = value;
                head_ = (head_ + 1) % array_.Length;

				bool setEmpty = (count_ == 0);

                count_ += 1;

				if (IsFull)
				{
					notFullEvent_.Reset();
				}

				if (setEmpty)
				{
					notEmptyEvent_.Set();
				}
            }

            bytesWritten_++;
        }

        public void Write(byte[] buffer, int index, int count)
        {
            if (isClosed_)
            {
                throw new ApplicationException("Buffer is closed");
            }

            while (count > 0)
            {
				notFullEvent_.WaitOne();

                // Gauranteed to not be full at this point, however readers may sill read
                // from the buffer first.
                lock (lockObject_)
                {
                    var bytesToWrite = Length - Count;

                    if (count < bytesToWrite)
                    {
                        bytesToWrite = count;
                    }
					bool setEmpty = (count_ == 0);

                    while (bytesToWrite > 0)
                    {
                        array_[head_] = buffer[index];
                        index++;

                        head_ = (head_ + 1) % array_.Length;

                        bytesToWrite--;
                        bytesWritten_++;
                        count--;
                        count_++;
                    }

					if (IsFull)
					{
						notFullEvent_.Reset();
					}

					if (setEmpty)
					{
						notEmptyEvent_.Set();
					}
                }
            }
        }

        /// <summary>
        /// Read a byte from the buffer.
        /// </summary>
        /// <returns></returns>
        public int ReadByte()
        {
            var result = -1;

			notEmptyEvent_.WaitOne();

            if (!IsEmpty)
            {
                lock (lockObject_)
                {
                    result = array_[tail_];
                    tail_ = (tail_ + 1) % array_.Length;
					bool setFull = IsFull;
                    count_ -= 1;
					if (!isClosed_ && (count_ == 0))
					{
						notEmptyEvent_.Reset();
					}

					if (setFull)
					{
						notFullEvent_.Set();
					}
                }
            }

            bytesRead_++;

            return result;
        }

        public int Read(byte[] buffer, int index, int count)
        {
            var result = 0;

            while (count > 0)
            {
				notEmptyEvent_.WaitOne();

                if (IsEmpty)
                {
                    count = 0;
                }
                else
                {
                    lock (lockObject_)
                    {
                        var toRead = Count;

                        if (toRead > count)
                        {
                            toRead = count;
                        }

                        result += toRead;

						bool setFull = IsFull;

                        while (toRead > 0)
                        {
                            buffer[index] = array_[tail_];
                            index++;

                            tail_ = (tail_ + 1) % array_.Length;
                            count--;
                            count_--;
                            toRead--;
                            bytesRead_++;
                        }
						if (!isClosed_ && (count_ == 0))
						{
							notEmptyEvent_.Reset();
						}

						if (setFull)
						{
							notFullEvent_.Set();
						}
                    }
                }
            }

            return result;
        }

        #region Properties

        /// <summary>
        /// Gets a value indicating wether the buffer is empty or not.
        /// </summary>
        public bool IsEmpty
        {
            get { return count_ == 0; }
        }

        public bool IsFull
        {
            get
            {
                return (count_ == array_.Length);
            }
        }

        public bool IsClosed
        {
            get { return isClosed_; }
        }

        /// <summary>
        /// Gets the number of elements in the buffer.
        /// </summary>
        public int Count
        {
            get
            {
                return count_;
            }
        }


        public int Length
        {
            get { return array_.Length; }
        }

        public long BytesWritten
        {
            get { return bytesWritten_; }
        }

        public long BytesRead
        {
            get { return bytesRead_; }
        }

        /// <summary>
        /// Indexer - Get an element from the tail of the RingBuffer.
        /// </summary>
        public byte this[int index]
        {
            get
            {
                if ((index < 0) || (index >= array_.Length))
                {
                    throw new ArgumentOutOfRangeException("index");
                }

                return array_[(tail_ + index) % array_.Length];
            }
        }

        #endregion

        #region Instance Variables
        /// <summary>
        /// Flag indicating the buffer is closed.
        /// </summary>
        bool isClosed_;

        /// <summary>
        /// Index for the head of the buffer.
        /// </summary>
        /// <remarks>Its the index of the next byte to be <see cref="Write">written</see>.</remarks>
        int head_;

        /// <summary>
        /// Index for the tail of the buffer.
        /// </summary>
        /// <remarks>Its the index of the next byte to be <see cref="Read">written</see>.</remarks>
        int tail_;

        /// <summary>
        /// The total number of elements added to the buffer.
        /// </summary>
        int count_;

        /// <summary>
        /// Storage for the ring buffer contents.
        /// </summary>
        readonly byte[] array_;

        long bytesWritten_;
        long bytesRead_;

        readonly object lockObject_;

        readonly ManualResetEvent notEmptyEvent_;
        readonly ManualResetEvent notFullEvent_;
        #endregion
    }

    [TestFixture]
    public class ExerciseBuffer
    {
        [Test]
        public void Basic()
        {
            const int Size = 64;

            buffer_ = new ReadWriteRingBuffer(Size);

            Assert.IsFalse(buffer_.IsFull);
            Assert.IsTrue(buffer_.IsEmpty);

            buffer_.WriteByte(1);

            Assert.IsFalse(buffer_.IsFull);
            Assert.IsFalse(buffer_.IsEmpty);
            Assert.AreEqual(1, buffer_.Count);

            Assert.AreEqual(1, buffer_.ReadByte());

            Assert.IsFalse(buffer_.IsFull);
            Assert.IsTrue(buffer_.IsEmpty);

            for (var i = 0; i < buffer_.Length; ++i)
            {
                buffer_.WriteByte(unchecked((byte)(i & 0xff)));
            }

            Assert.IsTrue(buffer_.IsFull);
            Assert.IsFalse(buffer_.IsEmpty);

            buffer_.Close();

            Assert.IsTrue(buffer_.IsClosed);

            var caught = false;
            try
            {
                buffer_.WriteByte(1);
            }
            catch
            {
                caught = true;
            }

            Assert.IsTrue(caught);

            var count = Size;
            var expected = 0;

            while (count != 0)
            {
                Assert.AreEqual(count, buffer_.Count);
                Assert.AreEqual(expected, buffer_.ReadByte());
                count--;
                expected = (expected + 1) & 0xff;
            }

            Assert.IsTrue(buffer_.IsEmpty);
            Assert.AreEqual(-1, buffer_.ReadByte());
        }

        [Test]
        public void Buffered()
        {

            const int Size = 64;

            buffer_ = new ReadWriteRingBuffer(Size);

            var writeBuffer = new byte[16];
            for (var i = 0; i < 16; ++i)
            {
                writeBuffer[i] = (byte)i;
            }

            buffer_.Write(writeBuffer, 0, 3);
            Assert.AreEqual(3, buffer_.Count);

            var readBuffer = new byte[16];
            Assert.AreEqual(3, buffer_.Read(readBuffer, 0, 3));
            for (var i = 0; i < 3; ++i)
            {
                Assert.AreEqual(i, readBuffer[i]);
            }

        }

        [Test]
        public void Threaded()
        {
            buffer_ = new ReadWriteRingBuffer(8);
            readTarget_ = writeTarget_ = 16384;

            var reader = new Thread(Reader);
            reader.Start();

            var writer = new Thread(Writer);
            writer.Start();

            writer.Join();
            reader.Join();
        }

        void Reader()
        {
            var r = new Random();
            byte nextValue = 0;

            while (readTarget_ > 0)
            {
                var thisTime = r.Next(16);
                if (thisTime > readTarget_)
                {
                    thisTime = readTarget_;
                }

                while (thisTime > 0)
                {
                    var readValue = buffer_.ReadByte();
                    Assert.AreEqual(nextValue, readValue);
                    nextValue = (byte)((nextValue + 1) & 0xff);
                    thisTime--;
                    readTarget_--;
                }

                Thread.Sleep(r.Next(10));

            }

            var last = buffer_.ReadByte();

            Assert.AreEqual(-1, last);
            Assert.IsTrue(buffer_.IsClosed);
        }

        void Writer()
        {
            var r = new Random();

            byte nextValue = 0;
            while (writeTarget_ > 0)
            {
                var thisTime = r.Next(16);
                if (thisTime > writeTarget_)
                {
                    thisTime = writeTarget_;
                }

                while (thisTime > 0)
                {
                    buffer_.WriteByte(nextValue);
                    nextValue = (byte)((nextValue + 1) & 0xff);
                    thisTime--;
                    writeTarget_--;
                }
                Thread.Sleep(r.Next(10));
            }
            buffer_.Close();
        }

        int readTarget_;
        int writeTarget_;

        ReadWriteRingBuffer buffer_;

    }
}
