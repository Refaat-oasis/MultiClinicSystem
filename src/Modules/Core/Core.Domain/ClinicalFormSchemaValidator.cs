using System.Text.Json;

namespace Core.Domain;

/// <summary>
/// Pure validator verifying that specialty clinical form answers satisfy a versioned JSON Schema definition.
/// </summary>
public static class ClinicalFormSchemaValidator
{
    public static ClinicalFormValidationResult Validate(string jsonSchema, string answersJson)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema))
        {
            return new ClinicalFormValidationResult(false, ["Clinical form schema cannot be empty."]);
        }

        if (string.IsNullOrWhiteSpace(answersJson))
        {
            return new ClinicalFormValidationResult(false, ["Clinical form answers cannot be empty."]);
        }

        JsonDocument schemaDoc;
        JsonDocument answersDoc;

        try
        {
            schemaDoc = JsonDocument.Parse(jsonSchema);
        }
        catch (JsonException)
        {
            return new ClinicalFormValidationResult(false, ["Clinical form schema is invalid JSON."]);
        }

        try
        {
            answersDoc = JsonDocument.Parse(answersJson);
        }
        catch (JsonException)
        {
            schemaDoc.Dispose();
            return new ClinicalFormValidationResult(false, ["Clinical form answers are invalid JSON."]);
        }

        using (schemaDoc)
        using (answersDoc)
        {
            if (schemaDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ClinicalFormValidationResult(false, ["Schema root must be a JSON object."]);
            }

            if (answersDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ClinicalFormValidationResult(false, ["Answers root must be a JSON object."]);
            }

            var errors = new List<string>();
            ValidateRequiredFields(schemaDoc.RootElement, answersDoc.RootElement, errors);
            ValidatePropertyTypes(schemaDoc.RootElement, answersDoc.RootElement, errors);

            return new ClinicalFormValidationResult(errors.Count == 0, errors);
        }
    }

    private static void ValidateRequiredFields(JsonElement schemaRoot, JsonElement answersRoot, List<string> errors)
    {
        if (!schemaRoot.TryGetProperty("required", out JsonElement requiredEl) || requiredEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement item in requiredEl.EnumerateArray())
        {
            string? fieldName = item.GetString();
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                continue;
            }

            if (!answersRoot.TryGetProperty(fieldName, out JsonElement val)
                || val.ValueKind == JsonValueKind.Null
                || (val.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(val.GetString())))
            {
                errors.Add($"Field '{fieldName}' is required.");
            }
        }
    }

    private static void ValidatePropertyTypes(JsonElement schemaRoot, JsonElement answersRoot, List<string> errors)
    {
        if (!schemaRoot.TryGetProperty("properties", out JsonElement propsEl) || propsEl.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty propSchema in propsEl.EnumerateObject())
        {
            if (!answersRoot.TryGetProperty(propSchema.Name, out JsonElement valueEl) || valueEl.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (!propSchema.Value.TryGetProperty("type", out JsonElement typeEl))
            {
                continue;
            }

            string? expectedType = typeEl.GetString();
            bool typeMatches = expectedType switch
            {
                "string" => valueEl.ValueKind == JsonValueKind.String,
                "number" => valueEl.ValueKind == JsonValueKind.Number,
                "integer" => valueEl.ValueKind == JsonValueKind.Number && valueEl.TryGetInt64(out _),
                "boolean" => valueEl.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => true
            };

            if (!typeMatches)
            {
                errors.Add($"Field '{propSchema.Name}' must be of type '{expectedType}'.");
                continue;
            }

            if (expectedType is "number" or "integer" && valueEl.TryGetDouble(out double numVal))
            {
                if (propSchema.Value.TryGetProperty("minimum", out JsonElement minEl) && minEl.TryGetDouble(out double minVal) && numVal < minVal)
                {
                    errors.Add($"Field '{propSchema.Name}' must be >= {minVal}.");
                }

                if (propSchema.Value.TryGetProperty("maximum", out JsonElement maxEl) && maxEl.TryGetDouble(out double maxVal) && numVal > maxVal)
                {
                    errors.Add($"Field '{propSchema.Name}' must be <= {maxVal}.");
                }
            }
        }
    }
}

