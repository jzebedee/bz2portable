using NUnit.Framework;

using ICSharpCode.SharpZipLib.Core;

// ReSharper disable once CheckNamespace
namespace ICSharpCode.SharpZipLib.Tests.Core
{
    [TestFixture]
    public class Core
    {

        [Test]
        public void FilterQuoting()
        {
            var filters = NameFilter.SplitQuoted("");
            Assert.AreEqual(0, filters.Length);

            filters = NameFilter.SplitQuoted(";;;");
            Assert.AreEqual(4, filters.Length);
            foreach (var filter in filters)
            {
                Assert.AreEqual("", filter);
            }

            filters = NameFilter.SplitQuoted("a;a;a;a;a");
            Assert.AreEqual(5, filters.Length);
            foreach (var filter in filters)
            {
                Assert.AreEqual("a", filter);
            }

            filters = NameFilter.SplitQuoted(@"a\;;a\;;a\;;a\;;a\;");
            Assert.AreEqual(5, filters.Length);
            foreach (var filter in filters)
            {
                Assert.AreEqual("a;", filter);
            }
        }

        [Test]
        public void NullFilter()
        {
            var nf = new NameFilter(null);
            Assert.IsTrue(nf.IsIncluded("o78i6bgv5rvu\\kj//&*"));
        }

        [Test]
        public void ValidFilter()
        {
            Assert.IsTrue(NameFilter.IsValidFilterExpression(null));
            Assert.IsTrue(NameFilter.IsValidFilterExpression(string.Empty));
            Assert.IsTrue(NameFilter.IsValidFilterExpression("a"));

            Assert.IsFalse(NameFilter.IsValidFilterExpression(@"\,)"));
            Assert.IsFalse(NameFilter.IsValidFilterExpression(@"[]"));
        }
    }
}
