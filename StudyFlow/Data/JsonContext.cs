using StudyFlow.Models;
using StudyFlow.Services.Yandex;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(Subject))]
[JsonSerializable(typeof(StudyTask))]
[JsonSerializable(typeof(SubjectsJson))]
[JsonSerializable(typeof(Tag))]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(YandexUser))]
[JsonSerializable(typeof(YandexTokenResponse))]
[JsonSerializable(typeof(YandexDiskInfo))]
public partial class JsonContext : JsonSerializerContext
{
}
