using System;
using System.IO;
using System.Threading;

using ICSharpCode.SharpZipLib.BZip2;

using ICSharpCode.SharpZipLib.Tests.TestSupport;

using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace ICSharpCode.SharpZipLib.Tests.BZip2
{
    /// <summary>
    /// This class contains test cases for Bzip2 compression
    /// </summary>
    [TestFixture]
    public class BZip2Suite
    {
        /// <summary>
        /// Basic compress/decompress test BZip2
        /// </summary>
        [Test]
        [Category("BZip2")]
        public void BasicRoundTrip()
        {
            var ms = new MemoryStream();
            var outStream = new BZip2OutputStream(ms);

            var buf = new byte[10000];
            var rnd = new Random();
            rnd.NextBytes(buf);

            outStream.Write(buf, 0, buf.Length);
            outStream.Close();
            ms = new MemoryStream(ms.GetBuffer());
            ms.Seek(0, SeekOrigin.Begin);

            using (var inStream = new BZip2InputStream(ms))
            {
                var buf2 = new byte[buf.Length];
                var pos = 0;
                while (true)
                {
                    var numRead = inStream.Read(buf2, pos, 4096);
                    if (numRead <= 0)
                    {
                        break;
                    }
                    pos += numRead;
                }

                for (var i = 0; i < buf.Length; ++i)
                {
                    Assert.AreEqual(buf2[i], buf[i]);
                }
            }
        }

        /// <summary>
        /// Check that creating an empty archive is handled ok
        /// </summary>
        [Test]
        [Category("BZip2")]
        public void CreateEmptyArchive()
        {
            var ms = new MemoryStream();
            var outStream = new BZip2OutputStream(ms);
            outStream.Close();
            ms = new MemoryStream(ms.GetBuffer());

            ms.Seek(0, SeekOrigin.Begin);

            using (var inStream = new BZip2InputStream(ms))
            {
                var buffer = new byte[1024];
                var pos = 0;
                while (true)
                {
                    var numRead = inStream.Read(buffer, 0, buffer.Length);
                    if (numRead <= 0)
                    {
                        break;
                    }
                    pos += numRead;
                }

                Assert.AreEqual(pos, 0);
            }
        }

        BZip2OutputStream outStream_;
        BZip2InputStream inStream_;
        WindowedStream window_;
        long readTarget_;
        long writeTarget_;

        [Test]
        [Category("BZip2")]
        public void Performance()
        {
            window_ = new WindowedStream(0x150000);

            outStream_ = new BZip2OutputStream(window_, 1);

            const long Target = 0x10000000;
            readTarget_ = writeTarget_ = Target;

            var reader = new Thread(Reader) { Name = "Reader" };

            var writer = new Thread(Writer) { Name = "Writer" };

            var startTime = DateTime.Now;
            writer.Start();

            inStream_ = new BZip2InputStream(window_);

            reader.Start();

            Assert.IsTrue(writer.Join(TimeSpan.FromMinutes(5.0D)));
            Assert.IsTrue(reader.Join(TimeSpan.FromMinutes(5.0D)));

            var endTime = DateTime.Now;
            var span = endTime - startTime;
            Console.WriteLine("Time {0} throughput {1} KB/Sec", span, (Target / 1024) / span.TotalSeconds);

        }

        void Reader()
        {
            const int Size = 8192;
            var readBytes = 1;
            var buffer = new byte[Size];

            var passifierLevel = readTarget_ - 0x10000000;

            while ((readTarget_ > 0) && (readBytes > 0))
            {
                var count = Size;
                if (count > readTarget_)
                {
                    count = (int)readTarget_;
                }

                readBytes = inStream_.Read(buffer, 0, count);
                readTarget_ -= readBytes;

                if (readTarget_ <= passifierLevel)
                {
                    Console.WriteLine("Reader {0} bytes remaining", readTarget_);
                    passifierLevel = readTarget_ - 0x10000000;
                }
            }

            Assert.IsTrue(window_.IsClosed, "Window should be closed");

            // This shouldnt read any data but should read the footer
            readBytes = inStream_.Read(buffer, 0, 1);
            Assert.AreEqual(0, readBytes, "Stream should be empty");
            Assert.AreEqual(0, window_.Length, "Window should be closed");
            inStream_.Close();
        }

        void WriteTargetBytes()
        {
            const int Size = 8192;

            var buffer = new byte[Size];

            while (writeTarget_ > 0)
            {
                var thisTime = Size;
                if (thisTime > writeTarget_)
                {
                    thisTime = (int)writeTarget_;
                }

                outStream_.Write(buffer, 0, thisTime);
                writeTarget_ -= thisTime;
            }
        }

        void Writer()
        {
            WriteTargetBytes();
            outStream_.Close();
        }
    }
}
