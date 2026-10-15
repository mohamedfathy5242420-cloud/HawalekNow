using HawalekNow.Application.Shared.Results;

namespace HawalekNow.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Success_has_no_error_and_generic_success_keeps_value()
    {
        Assert.True(Result.Success().IsSuccess);
        Assert.Null(Result.Success().Error);
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
        Assert.Null(Result<string?>.Success(null).Value);
    }

    [Theory]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.Conflict)]
    [InlineData(ErrorKind.Validation)]
    [InlineData(ErrorKind.Unauthorized)]
    [InlineData(ErrorKind.Forbidden)]
    public void Failure_has_exact_error_and_value_access_throws(ErrorKind kind)
    {
        var error = new Error(kind);
        var result = Result.Failure(error);
        var generic = Result<int>.Failure(error);
        Assert.False(result.IsSuccess);
        Assert.False(generic.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Same(error, generic.Error);
        Assert.Throws<InvalidOperationException>(() => generic.Value);
    }

    [Fact]
    public void Failure_without_error_and_unknown_error_kind_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Error((ErrorKind)999));
        Assert.Empty(typeof(Result).GetConstructors());
        Assert.Empty(typeof(Result<string>).GetConstructors());
        Assert.False(typeof(Result).GetProperty(nameof(Result.Error))!.CanWrite);
        Assert.False(typeof(Result<string>).GetProperty(nameof(Result<string>.IsSuccess))!.CanWrite);
    }
}
