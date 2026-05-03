using Moq;

namespace UnitTests.Helpers;

public static class MockHelpers
{
    public static Mock<T> CreateMock<T>() where T : class
        => new Mock<T>(MockBehavior.Strict);
}