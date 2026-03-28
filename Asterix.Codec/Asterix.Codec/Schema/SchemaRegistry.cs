using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Schema;

/// <summary>
/// Immutable runtime registry of validated ASTERIX category schemas and SPF field set definitions.
///
/// <para>
/// Constructed once at startup via <c>AsterixCodecBuilder</c>; all reads are thread-safe.
/// Mutation is only possible during the build phase (before <c>Build()</c> is called).
/// </para>
///
/// <para>
/// Full implementation — including merge strategy and schema loading integration — is part of
/// Phase 3 (Schema Loading &amp; Validation). This stub provides the interface required
/// by the decode and encode engines.
/// </para>
/// </summary>
public sealed class SchemaRegistry
{
    private readonly Dictionary<int, AsterixCategorySchema> _categories = new();
    private readonly Dictionary<string, SpfFieldSetDefinition> _spfFieldSets = new();
    private readonly Dictionary<int, StructuredExplicitItemSetSchema> _structuredExplicitItemSets = new();
    private volatile bool _frozen;

    #region Registry build operations
    /// <summary>
    /// Registers a validated category schema. Must be called before <see cref="Freeze"/>.
    /// </summary>
    public void RegisterCategory(AsterixCategorySchema schema)
    {
        ThrowIfFrozen();
        if (_categories.ContainsKey(schema.Category))
            throw new InvalidOperationException(
                $"A schema for CAT{schema.Category:D3} is already registered. " +
                "Each category may only be registered once per registry.");
        _categories[schema.Category] = schema;
    }

    /// <summary>
    /// Registers all SPF field sets from a validated schema. Must be called before <see cref="Freeze"/>.
    /// </summary>
    public void RegisterSpfFieldSets(SpfFieldSetSchema schema)
    {
        ThrowIfFrozen();
        foreach (var kvp in schema.FieldSets)
            _spfFieldSets[kvp.Key] = kvp.Value;
    }

    /// <summary>
    /// Registers all structured-explicit item definitions from a validated schema. Must be called before <see cref="Freeze"/>.
    /// </summary>
    public void RegisterStructuredExplicitItemSet(StructuredExplicitItemSetSchema schema)
    {
        ThrowIfFrozen();
        if (_structuredExplicitItemSets.ContainsKey(schema.Category))
            throw new InvalidOperationException(
                $"A structured-explicit item set for CAT{schema.Category:D3} is already registered. " +
                "Each category may only have one structured-explicit item set.");
        _structuredExplicitItemSets[schema.Category] = schema;
    }

    /// <summary>
    /// Seals the registry. Resolves any structured-explicit item sets into their category schemas,
    /// then prevents further registration.
    /// </summary>
    public void Freeze()
    {
        ResolveStructuredExplicitItems();
        _frozen = true;
    }

    private void ResolveStructuredExplicitItems()
    {
        foreach (var kvp in _structuredExplicitItemSets)
        {
            int cat = kvp.Key;
            var structuredExplicitSet = kvp.Value;

            if (!_categories.TryGetValue(cat, out var catSchema))
                throw new InvalidOperationException(
                    $"Structured-explicit item set registered for CAT{cat:D3} " +
                    "but no category schema is registered for that category.");

            // Validate that each structured-explicit item ID exists as ExplicitItemDefinition
            foreach (var itemId in structuredExplicitSet.Items.Keys)
            {
                if (!catSchema.Items.TryGetValue(itemId, out var def) || def is not ExplicitItemDefinition)
                    throw new InvalidOperationException(
                        $"Structured-explicit item '{itemId}' for CAT{cat:D3} must be defined as " +
                        "'type: explicit' in the category schema.");
            }

            // Rebuild items dict substituting matching explicit items with structured-explicit definitions
            var newItems = new Dictionary<string, ItemDefinition>(catSchema.Items.Count, StringComparer.Ordinal);
            foreach (var itemKvp in catSchema.Items)
            {
                newItems[itemKvp.Key] = structuredExplicitSet.Items.TryGetValue(itemKvp.Key, out var transDef)
                    ? (ItemDefinition)transDef
                    : itemKvp.Value;
            }

            _categories[cat] = catSchema.WithItems(newItems);
        }
    }
    
    #endregion

    #region Registry read operations

    /// <summary>
    /// Returns true and sets <paramref name="schema"/> if a schema is registered for
    /// <paramref name="category"/>.
    /// </summary>
    public bool TryGetCategory(int category, out AsterixCategorySchema? schema) =>
        _categories.TryGetValue(category, out schema);

    /// <summary>
    /// Returns true and sets <paramref name="definition"/> if an SPF field set is registered
    /// under <paramref name="name"/>.
    /// </summary>
    public bool TryGetSpfFieldSet(string name, out SpfFieldSetDefinition? definition) =>
        _spfFieldSets.TryGetValue(name, out definition);
    
    #endregion

    #region Helpers

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException(
                "SchemaRegistry is frozen; registration must occur before Build() is called");
    }
    #endregion
}
