using System.Text.Json.Serialization;

namespace Hoho.Decomp {
	/// <summary>
	/// NPM package info model for JSON deserialization.
	/// </summary>
	public sealed record NpmPackageInfo(
		string? Name,
		[property: JsonPropertyName("dist-tags")]
		DistTagsInfo? DistTags
	);

	public sealed record DistTagsInfo(
		string? Latest
	);

	/// <summary>
	/// AOT-compatible JSON source generation context.
	/// </summary>
	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower), JsonSerializable(typeof(NpmPackageInfo)), JsonSerializable(typeof(DistTagsInfo))]
	public partial class JsonContext : JsonSerializerContext { }
}