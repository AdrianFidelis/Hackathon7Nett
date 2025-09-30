using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using System.Text.Json;

namespace VideoApi.Tests;

public class UnitTest1
{
	private static VideoController CreateController() =>
		new VideoController(
			new InMemoryStore(),
			new NullLogger<VideoController>(),
			new ConfigurationBuilder().Build());

	[Fact]
	public void Results_Returns_File_From_Uploads_Results_When_Present()
	{
		// Arrange
		var controller = CreateController();
		var id = Guid.NewGuid().ToString("N");
		var baseDir = Directory.GetCurrentDirectory();
		var dir = Path.Combine(baseDir, "uploads", "results");
		Directory.CreateDirectory(dir);
		var file = Path.Combine(dir, id + ".json");
		var expected = JsonSerializer.Serialize(new[] { new { Content = "qr1", TimestampSeconds = 2 } });
		File.WriteAllText(file, expected);

		// Act
		var result = controller.Results(id) as ContentResult;

		// Assert
		result.Should().NotBeNull();
		result!.ContentType.Should().Be("application/json");
		result.Content.Should().Contain("qr1");
	}

	[Fact]
	public void Results_Returns_File_From_Results_When_Present()
	{
		// Arrange
		var controller = CreateController();
		var id = Guid.NewGuid().ToString("N");
		var baseDir = Directory.GetCurrentDirectory();
		var dir = Path.Combine(baseDir, "results");
		Directory.CreateDirectory(dir);
		var file = Path.Combine(dir, id + ".json");
		var expected = JsonSerializer.Serialize(new[] { new { Content = "qr2", TimestampSeconds = 5 } });
		File.WriteAllText(file, expected);

		// Act
		var result = controller.Results(id) as ContentResult;

		// Assert
		result.Should().NotBeNull();
		result!.ContentType.Should().Be("application/json");
		result.Content.Should().Contain("qr2");
	}

	[Fact]
	public void Results_FallsBack_To_Memory_When_No_File()
	{
		// Arrange
		var store = new InMemoryStore();
		var id = Guid.NewGuid().ToString("N");
		var list = new List<QRCodeResult> { new("mem-qrcode", 3) };
		store.Results[id] = list;

		var controller = new VideoController(
			store,
			new NullLogger<VideoController>(),
			new ConfigurationBuilder().Build());

		// Act
		var result = controller.Results(id) as OkObjectResult;

		// Assert
		result.Should().NotBeNull();
		var payload = result!.Value as IEnumerable<QRCodeResult>;
		payload.Should().NotBeNull();
		payload!.First().Content.Should().Be("mem-qrcode");
	}

	[Fact]
	public void Results_Returns_EmptyArray_When_NotFound()
	{
		// Arrange
		var controller = CreateController();
		var id = Guid.NewGuid().ToString("N");

		// Act
		var result = controller.Results(id) as OkObjectResult;

		// Assert
		result.Should().NotBeNull();
		var payload = result!.Value as IEnumerable<object>;
		payload.Should().NotBeNull();
		payload!.Should().BeEmpty();
	}

	
	[Fact]
	public void Status_Returns_NotFound_When_No_File_And_No_Memory()
	{
		// Arrange
		var controller = CreateController();
		var id = Guid.NewGuid().ToString("N");

		// Act
		var result = controller.Status(id);

		// Assert
		result.Should().BeOfType<NotFoundResult>();
	}
}