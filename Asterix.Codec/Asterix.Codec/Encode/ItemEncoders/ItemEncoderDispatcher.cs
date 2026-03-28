using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Routes a <see cref="DecodedItem"/> to the correct item encoder based on the
/// concrete type of both the item and the schema definition.
///
/// <para>
/// A static dispatcher is used instead of virtual dispatch or an interface because
/// encoders operate on <see cref="BitWriter"/> (a class) and the decoder-side
/// limitation of <c>ref struct</c> parameters does not apply here. The pattern
/// still mirrors <c>ItemDecoderDispatcher</c> for consistency.
/// </para>
/// </summary>
internal static class ItemEncoderDispatcher
{
    internal static void Encode(
        BitWriter writer,
        DecodedItem item,
        ItemDefinition definition,
        string itemPath)
    {
        switch (item, definition)
        {
            case (FixedDecodedItem fixedItem, FixedItemDefinition fixedDef):
                FixedItemEncoder.Encode(writer, fixedItem, fixedDef, itemPath);
                break;
            case (CompoundDecodedItem compoundItem, CompoundItemDefinition compoundDef):
                CompoundItemEncoder.Encode(writer, compoundItem, compoundDef, itemPath);
                break;
            case (RepetitiveDecodedItem repetitiveItem, RepetitiveItemDefinition repetitiveDef):
                RepetitiveItemEncoder.Encode(writer, repetitiveItem, repetitiveDef, itemPath);
                break;
            case (VariableDecodedItem variableItem, VariableItemDefinition variableDef):
                VariableItemEncoder.Encode(writer, variableItem, variableDef, itemPath);
                break;
            case (StructuredExplicitDecodedItem seItem, StructuredExplicitItemDefinition structuredExplicitDef):
                StructuredExplicitItemEncoder.Encode(writer, seItem, structuredExplicitDef, itemPath);
                break;
            case (ExplicitDecodedItem explicitItem, ExplicitItemDefinition explicitDef):
                ExplicitItemEncoder.Encode(writer, explicitItem, explicitDef, itemPath);
                break;
            default:
                throw new EncodeException(itemPath,
                    $"Cannot encode item of type '{item.GetType().Name}' " +
                    $"with definition of type '{definition.GetType().Name}'");
        }
    }
}