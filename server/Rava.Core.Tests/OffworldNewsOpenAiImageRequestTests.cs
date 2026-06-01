using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsOpenAiImageRequestTests
{
    [Theory]
    [InlineData("gpt-image-1", "1792x1024", "1536x1024")]
    [InlineData("gpt-image-1.5", "1024x1792", "1024x1536")]
    [InlineData("gpt-image-1-mini", "1024x1024", "1024x1024")]
    [InlineData("dall-e-3", "1792x1024", "1792x1024")]
    [InlineData("dall-e-2", "1792x1024", "1024x1024")]
    public void ResolveSize_MapsForModelFamily(string model, string aspectSize, string expected)
    {
        Assert.Equal(expected, OffworldNewsOpenAiImageRequest.ResolveSize(model, aspectSize));
    }

    [Fact]
    public void BuildRequestBody_GptImageModel_OmitsResponseFormat()
    {
        var body = OffworldNewsOpenAiImageRequest.BuildRequestBody(
            "gpt-image-1",
            "A mining scene",
            "1792x1024");

        Assert.False(body.ContainsKey("response_format"));
        Assert.Equal("1536x1024", body["size"]);
        Assert.Equal("jpeg", body["output_format"]);
        Assert.Equal("medium", body["quality"]);
    }

    [Fact]
    public void BuildRequestBody_Dalle3_OmitsResponseFormat()
    {
        var body = OffworldNewsOpenAiImageRequest.BuildRequestBody(
            "dall-e-3",
            "A mining scene",
            "1792x1024");

        Assert.False(body.ContainsKey("response_format"));
        Assert.Equal("standard", body["quality"]);
        Assert.Equal("1792x1024", body["size"]);
        Assert.False(body.ContainsKey("output_format"));
    }

    [Fact]
    public void BuildRequestBody_UnknownModel_UsesMinimalParameters()
    {
        var body = OffworldNewsOpenAiImageRequest.BuildRequestBody(
            "some-future-model",
            "A mining scene",
            "1792x1024");

        Assert.False(body.ContainsKey("response_format"));
        Assert.False(body.ContainsKey("quality"));
        Assert.False(body.ContainsKey("output_format"));
        Assert.Equal("1536x1024", body["size"]);
    }
}
