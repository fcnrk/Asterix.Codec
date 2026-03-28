namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Identifies the item and field that carry the discriminating value in a
/// multi-message category (e.g. CAT253). The field's <see cref="Model.DecodedField.RawValue"/>
/// is converted to string and matched against each <see cref="MessageDefinition.Discriminator"/>
/// to select the UAP for decoding.
/// </summary>
public sealed class CategoryDiscriminator
{
    /// <summary>Item ID of the fixed item that carries the discriminating value (e.g. "I253_010").</summary>
    public string ItemId { get; }

    /// <summary>Name of the field within that item whose RawValue is used for matching.</summary>
    public string FieldName { get; }

    public CategoryDiscriminator(string itemId, string fieldName)
    {
        ItemId = itemId;
        FieldName = fieldName;
    }
}
