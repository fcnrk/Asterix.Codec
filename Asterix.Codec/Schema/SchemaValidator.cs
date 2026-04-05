using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Schema;

/// <summary>
/// Validates cross-references and semantic constraints in loaded schema objects.
/// Called by <see cref="YamlSchemaLoader"/> after deserialization, before returning any schema.
/// All checks are fail-fast: the first violation throws <see cref="SchemaValidationException"/>.
/// </summary>
internal static class SchemaValidator
{
    #region Category schema validation

    internal static void Validate(AsterixCategorySchema schema, string? sourceHint)
    {
        string path = sourceHint ?? $"CAT{schema.Category:D3}";

        ValidateDiscriminator(schema, path);

        foreach (var message in schema.Messages)
            ValidateUap(message, schema, path);

        foreach (var kvp in schema.Items)
            ValidateItem(kvp.Key, kvp.Value, schema, path);
    }

    private static void ValidateDiscriminator(AsterixCategorySchema schema, string path)
    {
        var disc = schema.MessageDiscriminator;

        if (disc is null)
        {
            if (schema.Messages.Count > 1)
                throw new SchemaValidationException(path, schema.Category,
                    "discriminator",
                    $"Category has {schema.Messages.Count} messages but no 'discriminator' is defined. " +
                    "Multi-message categories require a discriminator item and field.");
            return;
        }

        if (schema.Messages.Count <= 1)
            throw new SchemaValidationException(path, schema.Category,
                "discriminator",
                "A 'discriminator' is defined but the category has only one message. " +
                "Remove the discriminator or add more message definitions.");

        // All messages must have unique, non-empty discriminator values
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in schema.Messages)
        {
            if (string.IsNullOrEmpty(message.Discriminator))
                throw new SchemaValidationException(path, schema.Category,
                    $"messages.{message.Id}.discriminator",
                    $"Message '{message.Id}' must have a non-empty 'discriminator' value " +
                    "when the category defines a discriminator.");

            if (!seen.Add(message.Discriminator!))
                throw new SchemaValidationException(path, schema.Category,
                    $"messages.{message.Id}.discriminator",
                    $"Duplicate discriminator value '{message.Discriminator}' in message '{message.Id}'.");
        }

        // Discriminator item must exist and be FixedItemDefinition
        if (!schema.Items.TryGetValue(disc.ItemId, out var discItemDef))
            throw new SchemaValidationException(path, schema.Category,
                $"discriminator.item",
                $"Discriminator item '{disc.ItemId}' is not defined in 'items'.");

        if (discItemDef is not FixedItemDefinition fixedDiscItem)
            throw new SchemaValidationException(path, schema.Category,
                $"discriminator.item",
                $"Discriminator item '{disc.ItemId}' must be of type 'fixed'.");

        if (!fixedDiscItem.Fields.Any(f => f.Name == disc.FieldName))
            throw new SchemaValidationException(path, schema.Category,
                $"discriminator.field",
                $"Discriminator field '{disc.FieldName}' not found in item '{disc.ItemId}'.");

        // Discriminator item must be first in every message's UAP
        foreach (var message in schema.Messages)
        {
            if (message.Uap.Count == 0 || message.Uap[0] != disc.ItemId)
                throw new SchemaValidationException(path, schema.Category,
                    $"messages.{message.Id}.uap[0]",
                    $"Message '{message.Id}' UAP must list '{disc.ItemId}' as the first entry " +
                    "(discriminator item must always be present and first).");
        }
    }

    private static void ValidateUap(
        MessageDefinition message,
        AsterixCategorySchema schema,
        string path)
    {
        for (int i = 0; i < message.Uap.Count; i++)
        {
            var itemId = message.Uap[i];
            if (!schema.Items.ContainsKey(itemId))
                throw new SchemaValidationException(path, schema.Category,
                    $"{message.Id}.uap[{i}]",
                    $"UAP references item '{itemId}' which is not defined in 'items'.");
        }
    }

    private static void ValidateItem(
        string itemId,
        ItemDefinition definition,
        AsterixCategorySchema schema,
        string path)
    {
        if (definition is CompoundItemDefinition compound)
            ValidateCompound(itemId, compound, schema, path);

        if (definition is FspecRepetitiveItemDefinition fspecRep)
            ValidateItem($"{itemId}.element", fspecRep.Element, schema, path);
    }

    private static void ValidateCompound(
        string itemId,
        CompoundItemDefinition compound,
        AsterixCategorySchema schema,
        string path)
    {
        for (int i = 0; i < compound.Fspec.Count; i++)
        {
            var subId = compound.Fspec[i];
            if (!compound.Subitems.ContainsKey(subId))
                throw new SchemaValidationException(path, schema.Category,
                    $"items.{itemId}.fspec[{i}]",
                    $"Compound fspec references subitem '{subId}' which is not defined in 'subitems'.");
        }

        // Recurse into nested compound subitems
        foreach (var kvp in compound.Subitems)
            if (kvp.Value is CompoundItemDefinition nested)
                ValidateCompound($"{itemId}.subitems.{kvp.Key}", nested, schema, path);
    }
    #endregion

    #region Structured-explicit item set validation

    internal static void Validate(StructuredExplicitItemSetSchema schema, string? sourceHint)
    {
        string path = sourceHint ?? $"structured_explicit_cat{schema.Category:D3}";

        foreach (var kvp in schema.Items)
            ValidateStructuredExplicitItem(kvp.Key, kvp.Value, path);
    }

    private static void ValidateStructuredExplicitItem(
        string itemId,
        StructuredExplicitItemDefinition def,
        string path)
    {
        if (def.Content.Count == 0)
            throw new SchemaValidationException(path, null,
                $"items.{itemId}.content",
                $"Structured-explicit item '{itemId}' has an empty 'content' list.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < def.Content.Count; i++)
        {
            var entry = def.Content[i];
            string entryPath = $"items.{itemId}.content[{i}]({entry.Id})";

            if (!seen.Add(entry.Id))
                throw new SchemaValidationException(path, null,
                    entryPath,
                    $"Duplicate content entry id '{entry.Id}' in structured-explicit item '{itemId}'.");

            // Recurse for compound inner items
            if (entry.Definition is CompoundItemDefinition compound)
            {
                // Check fspec vs subitems
                for (int j = 0; j < compound.Fspec.Count; j++)
                {
                    var subId = compound.Fspec[j];
                    if (!compound.Subitems.ContainsKey(subId))
                        throw new SchemaValidationException(path, null,
                            $"{entryPath}.fspec[{j}]",
                            $"Compound fspec references subitem '{subId}' which is not defined in 'subitems'.");
                }
            }
        }
    }

    #endregion

    #region SPF validation

    internal static void Validate(SpfFieldSetSchema schema, string? sourceHint)
    {
        string path = sourceHint ?? "spf";

        foreach (var kvp in schema.FieldSets)
            ValidateSpfFieldSet(kvp.Key, kvp.Value, path);
    }

    private static void ValidateSpfFieldSet(
        string setName,
        SpfFieldSetDefinition definition,
        string path)
    {
        var seenScalars = new HashSet<string>(StringComparer.Ordinal);
        var seenPresenceGroups = new Dictionary<string, DynamicPresenceEntry>(StringComparer.Ordinal);

        for (int i = 0; i < definition.Structure.Count; i++)
        {
            var entry = definition.Structure[i];
            string entryPath = $"{setName}.structure[{i}]({entry.Name})";

            switch (entry)
            {
                case ScalarEntry scalar:
                    if (scalar.Bits <= 0)
                        throw new SchemaValidationException(path, null, entryPath,
                            $"ScalarEntry '{scalar.Name}' has invalid bits={scalar.Bits}. Must be > 0.");
                    seenScalars.Add(scalar.Name);
                    break;

                case SpfRepetitiveEntry rep:
                    if (!seenScalars.Contains(rep.CountRef))
                        throw new SchemaValidationException(path, null, entryPath,
                            $"SpfRepetitiveEntry '{rep.Name}' references count_ref='{rep.CountRef}' " +
                            $"which is not a ScalarEntry defined before this entry.");
                    break;

                case DynamicPresenceEntry presence:
                    seenPresenceGroups[presence.Name] = presence;
                    break;

                case OptionalEntry optional:
                    if (!seenPresenceGroups.TryGetValue(optional.PresenceGroup, out var presGroup))
                        throw new SchemaValidationException(path, null, entryPath,
                            $"OptionalEntry '{optional.Name}' references presence group '{optional.PresenceGroup}' " +
                            $"which is not a DynamicPresenceEntry defined before this entry.");
                    if (!presGroup.Fields.Contains(optional.PresenceField))
                        throw new SchemaValidationException(path, null, entryPath,
                            $"OptionalEntry '{optional.Name}' references presence field '{optional.PresenceField}' " +
                            $"which is not listed in DynamicPresenceEntry '{optional.PresenceGroup}'.");
                    if (optional.Field.Bits <= 0)
                        throw new SchemaValidationException(path, null, entryPath,
                            $"OptionalEntry '{optional.Name}' has invalid bits={optional.Field.Bits}. Must be > 0.");
                    break;
            }
        }
    }
    #endregion
}
