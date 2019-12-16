using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using ServiceFabric.Mocks;

namespace SoCreate.Extensions.Caching.Tests
{
    public class AutoMoqDataAttribute : AutoDataAttribute
    {
        public AutoMoqDataAttribute() : base(() =>
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization {GenerateDelegates = true});
            fixture.Register(() => MockStatefulServiceContextFactory.Default);
            return fixture;
        })
        {
        }
    }
}
