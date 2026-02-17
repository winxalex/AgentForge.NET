﻿using Chat2Report.Options;
using Cloud.Unum.USearch;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using System.Collections.Concurrent;
using System.Linq.Expressions; // Required for expression compilation
using System.Reflection; // Required for reflection

namespace Chat2Report.VectorStore
{





    /// <summary>
    /// Implementation of IVectorStore for USearch.
    /// Manages the creation of USearchCollection instances.
    /// Assumes USearchIndex and IDataStore lifetimes are managed externally
    /// and passed in during construction or collection retrieval via the specific overload.
    /// This implementation doesn't inherently manage multiple named collections persistence.
    /// </summary>
    public class USearchVectorStore : IVectorStore, IDisposable
    {
        // Note: This basic implementation doesn't manage named collections persistently.
        // It relies on external provision of USearchIndex and IDataStore instances
        // for a specific, implicitly named collection via the overload below.

       

        private readonly ConcurrentDictionary<string, object> _collections = new();


        private bool _disposed;
        private ILogger<USearchVectorStore> __logger;

        internal string IndexPersistencePath { get; }

        private readonly ConcurrentDictionary<string, USearchIndex> __indices = new();

        public ConcurrentDictionary<string, object> Collections => _collections;

       

       

        public Chat2Report.Options.IndexOptions IndexOptions { get; }

        private readonly IServiceProvider __dataStoreProvider;



        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serviceProvider"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public USearchVectorStore(IOptions<VectorStoreSettings> settings, IServiceProvider serviceProvider, ILogger<USearchVectorStore> logger = null)
        {
            var vectorStoreSettings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            var indexOptions = vectorStoreSettings.IndexSettings ?? throw new InvalidOperationException("IndexSettings section is missing in VectorStoreSettings.");

            // --- Validation ---
            if (string.IsNullOrWhiteSpace(indexOptions.IndexPersistencePath))
            {
                throw new ArgumentException("Index persistence path cannot be null or empty.", $"{nameof(settings)}.{nameof(VectorStoreSettings.IndexSettings)}.{nameof(IndexOptions.IndexPersistencePath)}");
            }
            if (indexOptions.VectorDimension <= 0)
            {
                throw new ArgumentException("Vector dimension must be greater than zero.", $"{nameof(settings)}.{nameof(VectorStoreSettings.IndexSettings)}.{nameof(IndexOptions.VectorDimension)}");
            }
            if (indexOptions.SearchFetchMultiplier <= 0)
            {
                throw new ArgumentException("SearchFetchMultiplier must be greater than zero.", $"{nameof(settings)}.{nameof(VectorStoreSettings.IndexSettings)}.{nameof(IndexOptions.SearchFetchMultiplier)}");
            }
            if (indexOptions.SearchBaseFetchCount < 0) // Can be 0, but not negative
            {
                throw new ArgumentException("SearchBaseFetchCount cannot be negative.", $"{nameof(settings)}.{nameof(VectorStoreSettings.IndexSettings)}.{nameof(IndexOptions.SearchBaseFetchCount)}");
            }
            // --- End Validation ---

            __logger= logger ??= LoggerFactory.Create(b=>b.AddDebug().AddConsole()).CreateLogger<USearchVectorStore>();


            // Resolve the persistence path. If it's a relative path, make it relative
            // to the application's base directory to ensure it works correctly in
            // different hosting environments (e.g., console app vs. web app).
            var persistencePath = indexOptions.IndexPersistencePath;
            if (!Path.IsPathRooted(persistencePath))
            {
                persistencePath = Path.GetFullPath(persistencePath, AppContext.BaseDirectory);
            }
            
            IndexPersistencePath = persistencePath;

            indexOptions.IndexPersistencePath = IndexPersistencePath; // Update the options with the resolved path

            IndexOptions = indexOptions;

            __dataStoreProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            
        }

        // Helper method in USearchVectorStore for index removal
        internal bool RemoveIndex(string collectionName, out USearchIndex index)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or empty.", nameof(collectionName));
            }

            //Delete the index file if it exists

            string indexPath = GetIndexPath(collectionName);
           
            if (File.Exists(indexPath))
            {
                try
                {
                    File.Delete(indexPath);
                    __logger.LogInformation($"Index file for collection '{collectionName}' deleted successfully.");
                }
                catch (Exception ex)
                {
                    __logger.LogError($"Failed to delete index file for collection '{collectionName}': {ex.Message}");
                }
            }

            return __indices.TryRemove(collectionName, out index);
        }

        public IDataStore<ulong, TRecord> GetDataStore<TRecord>()
        {
            return __dataStoreProvider.GetRequiredService<IDataStore<ulong, TRecord>>(); // Ensure the data store is available
        }

        internal USearchIndex GetOrCreateIndex(string collectionName)
        {
            return __indices.GetOrAdd(collectionName, name =>
            {
                string indexPath = GetIndexPath(name);
                if (File.Exists(indexPath))
                {

                    return new USearchIndex(indexPath);
                }
                else
                {
                    return new USearchIndex(
                        metricKind: IndexOptions.MetricKind,
                        quantization: IndexOptions.Quantization,
                        dimensions: (ulong)IndexOptions.VectorDimension,
                        connectivity: IndexOptions.Connectivity,
                        expansionAdd: IndexOptions.ExpansionAdd,
                        expansionSearch: IndexOptions.ExpansionSearch
                    );
                }
            });
        }


        internal string GetIndexPath(string collectionName)
        {
            return Path.Combine(IndexOptions.IndexPersistencePath, $"{collectionName}.usearch");
        }

        /// <summary>
        ///  Look like factory method to create a collection object. Probably peristent storage is implement in collection methods CreateCollectionAsync and CreateCollectionIfNotExistsAsync.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TRecord"></typeparam>
        /// <param name="name"></param>
        /// <param name="vectorStoreRecordDefinition"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
            string name,
            VectorStoreRecordDefinition? vectorStoreRecordDefinition = null)


        {

            // Validate TKey compatibility (USearch C# binding uses ulong)
            if (typeof(TKey) != typeof(ulong))
            {
                throw new ArgumentException($"USearch C# binding currently supports only ulong keys. Provided TKey: {typeof(TKey).Name} for collection '{name}'", nameof(TKey));
            }


            // Create a new collection if it doesn't exist
            IVectorStoreRecordCollection<TKey, TRecord> vectorStoreRecordCollection =
            (IVectorStoreRecordCollection<TKey, TRecord>)new USearchCollection<TRecord>(
                this,
                name, // Pass the collection name
                vectorStoreRecordDefinition

            );



            return vectorStoreRecordCollection;
        }

        /// <summary>
        /// Retrieves the names of all the collections known to this vector store instance.
        /// </summary>
        /// <remarks>
        /// Since this basic USearchVectorStore implementation doesn't manage a persistent
        /// list of named collections, this method currently returns the keys of the in-memory dictionary.
        /// </remarks>
        public async IAsyncEnumerable<string> ListCollectionNamesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {

            var collectionAsync = _collections.Keys.ToAsyncEnumerable();

            await foreach (var key in collectionAsync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;
            }
        }

        // Implement IDisposable to clean up resources
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var kvp in __indices)
            {
                try
                {
                    string indexPath = GetIndexPath(kvp.Key);
                    kvp.Value.Save(indexPath);
                    __logger.LogInformation($"USearch index for collection '{kvp.Key}' saved to {indexPath}.");
                }
                catch (Exception ex)
                {
                    __logger.LogError($"Error saving USearch index for collection '{kvp.Key}': {ex.Message}");
                }
                finally
                {
                    kvp.Value.Dispose();
                   
                    
                }
            }

           
        }
    }


    /// <summary>
    /// Implementation of IVectorStoreRecordCollection and related search interfaces for USearch.
    /// </summary>
    /// <typeparam name="TRecord">The Type of the data record.</typeparam>
    public class USearchCollection<TRecord> :
        IVectorStoreRecordCollection<ulong, TRecord>,
        // Interface for vector-only search
        IKeywordHybridSearch<TRecord>               // Interface for hybrid search


    {

        private readonly USearchVectorStore __vectorStore;
        private readonly USearchIndex __index;
        private readonly IDataStore<ulong, TRecord> __dataStore;
        private readonly VectorStoreRecordDefinition __vectorStoreDefinition;
        private const int DefaultSearchCount = 1000; // How many results to fetch initially for filtering
                                                     // Cache compiled property getters to avoid recompilation
        static readonly ConcurrentDictionary<string, Delegate> __propertyGetterCache = new();
        private ILogger<USearchCollection<TRecord>> __logger;

        public string CollectionName { get; }

        public USearchCollection(USearchVectorStore vectorStore, string name, VectorStoreRecordDefinition vectorStoreRecordDefinition = null)
        {
            __vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));

            

            __dataStore = vectorStore.GetDataStore<TRecord>();

            __vectorStoreDefinition = vectorStoreRecordDefinition;
            CollectionName = string.IsNullOrEmpty(name) ? "default" : name; // Store the name, provide default

            // Get the specific index for this collection
            __index = __vectorStore.GetOrCreateIndex(CollectionName);



            // Create logger for this collection using the vector store's logger factory
            var loggerFactory = LoggerFactory.Create(b => b.AddDebug().AddConsole());

            __logger = loggerFactory.CreateLogger<USearchCollection<TRecord>>();


        }

        // --- IVectorStoreRecordCollection Implementation ---

        public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        {

            return await __dataStore.ExistAsync(CollectionName, cancellationToken);
        }

        public async Task CreateCollectionAsync(CancellationToken cancellationToken = default)
        {

            //add table or other data store creation logic here
            await __dataStore.EnsureCreatedAsync(CollectionName, cancellationToken);

        }

        public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            if (!await CollectionExistsAsync(cancellationToken))
            {
                await CreateCollectionAsync(cancellationToken);
            }
        }



        public async Task<TRecord?> GetAsync(ulong key, GetRecordOptions? options = default, CancellationToken cancellationToken = default)
        {
            return await __dataStore.GetAsync(key, CollectionName, cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<TRecord> GetBatchAsync(
            IEnumerable<ulong> keys,

            GetRecordOptions? options = default,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var keyRecordPairsEnumerable = await __dataStore.GetBatchAsync(keys, CollectionName, cancellationToken).ConfigureAwait(false);

            var asyncKeyRecordPairs = keyRecordPairsEnumerable.ToAsyncEnumerable();

            // Use await foreach on the resolved IAsyncEnumerable<TRecord?>
            await foreach (var keyRecordPair in asyncKeyRecordPairs)
            {
                if (keyRecordPair.Value != null)
                {
                    yield return keyRecordPair.Value;
                }
            }
        }

        public async Task<ulong> UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
        {
            var (key, vector) = GetKeyAndVectorNullify(record);

           
            //Check if the vector is already in the index
            if (__index.Get(key, out float[] vectorData) > 0)
            {
                __index.Remove(key);
                __logger.LogInformation($"Vector with {key}  already exists, removing old vector from index.");
            }

            __index.Add(key, vector.ToArray());
            await __dataStore.UpsertAsync(key, CollectionName, record, cancellationToken);


            return key;
        }

        public async IAsyncEnumerable<ulong> UpsertBatchAsync(
            IEnumerable<TRecord> records,

            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var keys = new List<ulong>();
            var vectors = new List<float[]>();
            var dataStoreBatch = new List<KeyValuePair<ulong, TRecord>>();

            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();



                var (key, vector) = GetKeyAndVectorNullify(record);

                if (vector.Length != (int)__index.Dimensions())
                {
                    __logger.LogError($"Vector dimension mismatch for record with key {key}. Expected {(int)__index.Dimensions()}, got {vector.Length}.");
                    throw new InvalidOperationException($"Vector dimension {vector.Length} mismatch {(int)__index.Dimensions()}");
                }

                keys.Add(key);
                vectors.Add(vector.ToArray());
                dataStoreBatch.Add(new KeyValuePair<ulong, TRecord>(key, record));
                yield return key;
            }

            if (keys.Count > 0)
            {
                foreach (var key in keys)
                {
                    if (__index.Get(key, out float[] vector) > 0)
                    {
                        __index.Remove(key);
                        __logger.LogInformation($"Vector for {key} already exist. Remove vector.");
                    }
                }
                __index.Add(keys.ToArray(), vectors.ToArray());
                await __dataStore.UpsertBatchAsync(dataStoreBatch, CollectionName, cancellationToken);
            }

            __index.Save(__vectorStore.GetIndexPath(CollectionName)); // Save the index after batch upsert
        
            Console.WriteLine($"Batch upsert completed for collection '{CollectionName}'. {string.Join(",",keys)}. {keys.Count} records processed.");

        }

        public async Task<VectorSearchResults<TRecord>> VectorizedSearchAsync<TVector>(TVector searchVector, VectorSearchOptions<TRecord> options = null, CancellationToken cancellationToken = default)
        {
            ReadOnlyMemory<float> searchVectorMemory;
            if (searchVector is ReadOnlyMemory<float> memory)
            {
                searchVectorMemory = memory;
            }
            else if (searchVector is float[] array)
            {
                searchVectorMemory = new ReadOnlyMemory<float>(array);
            }
            else
            {
                throw new ArgumentException("Search vector must be of type ReadOnlyMemory<float> or float[].", nameof(searchVector));
            }


            // Check if options contains a VectorProperty specification
            if (options?.VectorProperty != null)
            {
                throw new ArgumentException(
                    "VectorProperty specification is not supported. Only single vector property per record is supported. Vector are automatically managed using [VectorStoreRecordVector] attribute.",
                    nameof(options));
            }

            // Handle null options
            options ??= new VectorSearchOptions<TRecord>();

            // Ensure vector dimensions match
            if (__index.Dimensions() > 0 && searchVectorMemory.Length != (int)__index.Dimensions())
            {
                throw new ArgumentException($"Search vector dimension mismatch for collection '{CollectionName}'. Index expects {__index.Dimensions()}, provided {searchVectorMemory.Length}.", nameof(searchVectorMemory));
            }

            HybridSearchOptions<TRecord> hybridOptions = new HybridSearchOptionsWithScoreFiltering<TRecord>
            {
                Top = options.Top,
                Skip = options.Skip,
                IncludeVectors = options.IncludeVectors,
                Filter = options.Filter, // Pass through filter if provided,
                VectorProperty = options.VectorProperty, // Pass through vector property if provided



            };




            return await HybridSearchAsync(searchVector, Array.Empty<string>(), hybridOptions, cancellationToken);
        }


        /// <summary>
        /// Deletes a record by its key from both the USearch index and the data store.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task DeleteAsync(ulong key, CancellationToken cancellationToken = default)
        {
            // USearch C# binding might lack vector removal by key.
            __index.Remove(key);

            return __dataStore.DeleteAsync(key, CollectionName, cancellationToken);
        }

        /// <summary>
        /// Deletes multiple records by their keys from both the USearch index and the data store.
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteBatchAsync(IEnumerable<ulong> keys, CancellationToken cancellationToken = default)
        {

            // Remove keys from USearch index
            foreach (var key in keys)
            {
                __index.Remove(key);
            }
            // Perform data store deletion
            await __dataStore.DeleteBatchAsync(keys, CollectionName, cancellationToken).ConfigureAwait(false);
        }


        public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
        {

            // Get all keys from the data store to ensure complete cleanup
            var allRecords = await __dataStore.GetAllAsync(CollectionName, cancellationToken);
            var allKeys = allRecords.Select(r => r.Key).ToList();

            // Remove all vectors from the USearch index
            foreach (var key in allKeys)
            {
               __index.Remove(key);
            }



            __vectorStore.Collections.TryRemove(CollectionName, out _);

           __logger.LogInformation($"Collection '{CollectionName}' deleted. All vectors removed from index and data store records deleted.");

            // Save the index after deletion

            __vectorStore.RemoveIndex(CollectionName, out _);

            // Note: If you want to completely delete the index file, you could add:
            //if (File.Exists(__vectorStore.IndexPath))
            //{
            //    File.Delete(__vectorStore.IndexPath); 
            //}


            await __dataStore.DeleteBatchAsync(allKeys, CollectionName, cancellationToken);
        }

        public async Task<VectorSearchResults<TRecord>> HybridSearchAsync<TVector>(TVector searchVector, ICollection<string> keywords, HybridSearchOptions<TRecord> options = null, CancellationToken cancellationToken = default)
        {

            // --- 1. Validate Input and Prepare Search Vector ---
            ReadOnlyMemory<float> searchVectorMemory = ValidateAndGetSearchVector(searchVector);

            // Ensure vector dimensions match
            if (__index.Dimensions() > 0 && searchVectorMemory.Length != (int)__index.Dimensions())
            {
                throw new ArgumentException($"Search vector dimension mismatch for collection '{CollectionName}'. Index expects {__index.Dimensions()}, provided {searchVectorMemory.Length}.", nameof(searchVector));
            }

            // --- 2. Perform Initial Vector Search ---
            int top = options.Top;
            int skip = options.Skip;
            // Use fetch parameters from IndexOptions
            int fetchCount = Math.Max(__vectorStore.IndexOptions.SearchBaseFetchCount, (top + skip) * __vectorStore.IndexOptions.SearchFetchMultiplier);

            int matchesCount = __index.Search(searchVectorMemory.ToArray(), fetchCount, out ulong[] foundKeys, out float[] foundDistances);

            if (matchesCount == 0)
            {
                return new VectorSearchResults<TRecord>(AsyncEnumerable.Empty<VectorSearchResult<TRecord>>());
            }

            // Compile score filter if provided
          
            Func<float, bool>? scoreFilterFunc = null;
            if (options is HybridSearchOptionsWithScoreFiltering<TRecord> filteringOptions)
            {
                scoreFilterFunc = CompileScoreFilter(filteringOptions.ScoreFilter);
            }

            // Build the result dictionary with optional score filtering
            var vectorResults = Enumerable.Range(0, matchesCount)
                .Select(i => new { Key = foundKeys[i], Distance = foundDistances[i] })
                .Where(r => scoreFilterFunc == null || scoreFilterFunc(r.Distance))
                .ToDictionary(r => r.Key, r => r.Distance);


            // 2. Retrieve corresponding records from the data store
            var recordsFromDataStore = new List<KeyValuePair<ulong, TRecord>>();
            var batchKeys = vectorResults.Keys;
            var retrievedRecords = await __dataStore.GetBatchAsync(batchKeys, CollectionName, cancellationToken);

            
            // retrievedRecords is now IEnumerable<KeyValuePair<ulong, TRecord>>
            recordsFromDataStore.AddRange(retrievedRecords);



            // 3. Apply Keyword Filtering
            IEnumerable<KeyValuePair<ulong, TRecord>> keywordFilteredRecords = recordsFromDataStore;
            if (options?.AdditionalProperty != null)
            {
                var propGetter = CompilePropertyGetter<object?>(options.AdditionalProperty);


                var keywordList = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? new List<string>();
                if (keywordList.Any())
                {

                    keywordFilteredRecords = recordsFromDataStore.Where(kvp =>
                    {
                        object? propValue = propGetter(kvp.Value);

                        if (propValue is IEnumerable<string> enumerable)
                        {
                            return enumerable.Any(kw => keywordList.Contains(kw, StringComparer.OrdinalIgnoreCase));
                        }

                        if (propValue is string str)
                        {
                            return keywordList.Any(kw => str.Contains(kw, StringComparison.OrdinalIgnoreCase));
                        }

                        return false; // No match if the property is not a string or IEnumerable<string>

                    });
                }
            }

            // 4. Apply Lambda Filter
            IEnumerable<KeyValuePair<ulong, TRecord>> lambdaFilteredRecords = keywordFilteredRecords;
            if (options?.Filter != null)
            {
                var filterFunc = options.Filter.Compile();
                lambdaFilteredRecords = keywordFilteredRecords.Where(kvp => filterFunc(kvp.Value));
            }






            // 5. Create final results
            var finalResults = lambdaFilteredRecords
                .Select(kvp => new VectorSearchResult<TRecord>(
                    //if (options.IncludeVectors) we retrive vector from Index and then apply to the record
                    options.IncludeVectors ? SetVectorEmbeddings(kvp.Value, RetrieveVectorFromIndex(kvp.Key)) : kvp.Value,
                    vectorResults[kvp.Key]

                ))
                .OrderBy(r => r.Score)
                .Skip(skip)
                .Take(top);

            return new VectorSearchResults<TRecord>(finalResults.ToAsyncEnumerable());

        }












        // --- Helper Methods ---
        private ReadOnlyMemory<float> RetrieveVectorFromIndex(ulong key)
        {
            __index.Get(key, out float[] vector);

            return new ReadOnlyMemory<float>(vector);
        }

        private bool DoesRecordMatchKeywords(TRecord record, Func<TRecord, object?> propertyGetter, List<string> keywordList)
        {
            if (!keywordList.Any()) return true; // No keywords? Always match.
            object? propValue = propertyGetter(record);

            if (propValue is IEnumerable<string> strEnum)
                return strEnum.Any(val => !string.IsNullOrEmpty(val) && keywordList.Contains(val, StringComparer.OrdinalIgnoreCase));
            if (propValue is string strValue && !string.IsNullOrEmpty(strValue))
                return keywordList.Any(kw => strValue.Contains(kw, StringComparison.OrdinalIgnoreCase));

            return false;
        }


        private ReadOnlyMemory<float> ValidateAndGetSearchVector<TVector>(TVector searchVector)
        {
            if (searchVector is ReadOnlyMemory<float> memory) return memory;
            if (searchVector is float[] array) return new ReadOnlyMemory<float>(array);
            throw new ArgumentException("Search vector must be of type ReadOnlyMemory<float> or float[].", nameof(searchVector));
        }


        private Func<float, bool>? CompileScoreFilter(Expression<Func<float, bool>>? scoreFilterExpression)
        {
            if (scoreFilterExpression == null) return null;
            try { return scoreFilterExpression.Compile(); }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not compile ScoreFilter expression: {ex.Message}. Filter ignored.");
                return null;
            }
        }


        /// <summary>
        /// Retrieves the key and vector from the record by reflection, but nullifies the vector property in the record. Vector is saved in the USearch index.
        /// This lightens the record for storage.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private (ulong Key, ReadOnlyMemory<float> Vector) GetKeyAndVectorNullify(TRecord record)
        {
            ulong? key = null;
            ReadOnlyMemory<float>? vector = null;
            PropertyInfo? keyProp = null;
            PropertyInfo? vecProp = null;

            foreach (var property in typeof(TRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)) // Ensure public instance properties
            {
                if (property.GetCustomAttribute<VectorStoreRecordKeyAttribute>() != null)
                {
                    if (keyProp != null) throw new InvalidOperationException($"Multiple properties marked with [{nameof(VectorStoreRecordKeyAttribute)}] in type {typeof(TRecord).Name}.");
                    if (property.PropertyType != typeof(ulong)) throw new InvalidOperationException($"Property '{property.Name}' marked with [{nameof(VectorStoreRecordKeyAttribute)}] must be of type ulong for USearch.");
                    keyProp = property;
                    key = (ulong?)property.GetValue(record);


                }
                else
                {
                    if (property.GetCustomAttribute<VectorStoreRecordVectorAttribute>() != null)
                    {
                        if (vecProp != null) throw new InvalidOperationException($"Multiple properties marked with [{nameof(VectorStoreRecordVectorAttribute)}] in type {typeof(TRecord).Name}.");
                        if (property.PropertyType != typeof(ReadOnlyMemory<float>)) throw new InvalidOperationException($"Property '{property.Name}' marked with [{nameof(VectorStoreRecordVectorAttribute)}] must be of type ReadOnlyMemory<float>.");
                        vecProp = property;
                        vector = (ReadOnlyMemory<float>?)property.GetValue(record);
                        property.SetValue(record, null); // Nullify the vector property in the record
                    }

                }
            }

            if (key == null || keyProp == null) throw new InvalidOperationException($"Record of type {typeof(TRecord).Name} must have exactly one public instance property marked with [{nameof(VectorStoreRecordKeyAttribute)}] of type ulong.");





            if (vector == null || vecProp == null) throw new InvalidOperationException($"Record of type {typeof(TRecord).Name} must have exactly one public instance property marked with [{nameof(VectorStoreRecordVectorAttribute)}] of type ReadOnlyMemory<float>.");

            return (key.Value, vector.Value);
        }

        private TRecord SetVectorEmbeddings(TRecord record, ReadOnlyMemory<float> vector)
        {
            foreach (var property in typeof(TRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)) // Ensure public instance properties
            {
                if (property.GetCustomAttribute<VectorStoreRecordVectorAttribute>() != null)
                {
                    if (property.PropertyType == typeof(ReadOnlyMemory<float>))
                    {

                        property.SetValue(record, vector);

                    }
                    else
                    {
                        Console.WriteLine($"Warning: Property '{property.Name}' marked with VectorStoreRecordVectorAttribute has incorrect type ({property.PropertyType}), expected ReadOnlyMemory<float>.");

                    }
                }
            }

            return record;

        }

        private Func<TRecord, TProp?> CompilePropertyGetter<TProp>(Expression<Func<TRecord, TProp>> expression)
        {


            try
            {
                // Ensure the expression body is a MemberExpression for direct property access
                if (expression.Body is MemberExpression memberExp && memberExp.Member is PropertyInfo propertyInfo)
                {
                    // Get the property Type
                    Type propertyType = propertyInfo.PropertyType;

                    // Validate that the property Type matches the expected Type
                    if (!typeof(TProp).IsAssignableFrom(propertyType))
                    {
                        throw new ArgumentException($"The property type '{propertyType}' is not assignable to the expected type '{typeof(TProp)}'.", nameof(expression));
                    }

                    if (propertyType != typeof(string) && !typeof(IEnumerable<string>).IsAssignableFrom(propertyType))
                    {
                        throw new InvalidOperationException($"Property must be of type string or IEnumerable<string>, but found {propertyType}.");
                    }

                    // Cache key based on property name and Type
                    string cacheKey = $"{typeof(TRecord).FullName}.{propertyInfo.Name}";

                    // Retrieve or compile the property getter
                    return (Func<TRecord, TProp?>)__propertyGetterCache.GetOrAdd(cacheKey, _ => expression.Compile());
                }
                else
                {
                    // Handle cases like r => r.Metadata["fieldName"] if necessary, or throw
                    throw new ArgumentException("Expression must be a simple property access (e.g., r => r.PropertyName)", nameof(expression));
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Could not compile property expression: {expression}. Ensure it's a valid property access.", nameof(expression), ex);
            }
        }



        //private Func<TRecord, TProp?> CompilePropertyGetter<TProp>(Expression<Func<TRecord, TProp>> expression)
        //{
        //    try
        //    {
        //        // Ensure the expression body is a MemberExpression for direct property access
        //        if (expression.Body is MemberExpression memberExp && memberExp.Member is PropertyInfo propertyInfo)
        //        {
        //            // Validate that the property Type matches the expected Type
        //            if (!typeof(TProp).IsAssignableFrom(propertyInfo.PropertyType))
        //            {
        //                throw new ArgumentException($"The property Type '{propertyInfo.PropertyType}' is not assignable to the expected Type '{typeof(TProp)}'.", nameof(expression));
        //            }

        //            return expression.Compile();
        //        }
        //        else
        //        {
        //            // Handle cases like r => r.Metadata["fieldName"] if necessary, or throw
        //            throw new ArgumentException("Expression must be a simple property access (e.g., r => r.PropertyName)", nameof(expression));
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new ArgumentException($"Could not compile property expression: {expression}. Ensure it's a valid property access.", nameof(expression), ex);
        //    }
        //}


    }
}
