using System.Text.Json.Serialization;
using System.Text.Json;
using System;

namespace QuickType.Model;

public readonly struct CaretRectangle
{
    public readonly long Left { get; }
    public readonly long Top { get; }
    public readonly long Width { get; }
    public readonly long Height { get; }
    public readonly long Right => Left + Width;
    public readonly long Bottom => Top + Height;

    public CaretRectangle(long left, long top, long width, long height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public override string ToString()
    {
        return $"CaretRectangle [Left={Left}, Top={Top}, Width={Width}, Height={Height}, Right={Right}, Bottom={Bottom}]";
    }
}

public class CaretRectangleJsonConverter : JsonConverter<CaretRectangle>
{
    public override CaretRectangle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        long left = 0, top = 0, width = 0, height = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "Left":
                    left = reader.GetInt64();
                    break;
                case "Top":
                    top = reader.GetInt64();
                    break;
                case "Width":
                    width = reader.GetInt64();
                    break;
                case "Height":
                    height = reader.GetInt64();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new CaretRectangle(left, top, width, height);
    }

    public override void Write(Utf8JsonWriter writer, CaretRectangle value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Left", value.Left);
        writer.WriteNumber("Top", value.Top);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        writer.WriteEndObject();
    }
}