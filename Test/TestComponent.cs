#if DEBUG
using System;
using System.Collections.Generic;
using BepInSerializer;
using UnityEngine;
using BepInSerializer.Core.Serialization.Attributes;
using BepInSerializer.Core.Serialization.Converters;

public class SerializationBridgeTester : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Bridge Data")]
    public BridgePayload payload;
    [SerializeField]
    private NonSerializablePayload nonSerializablePayload;
    [SerializeReference]
    private NonSerializablePayload refNonSerializablePayload;
    [SerializeField]
    protected string[] testStringArray;
    private List<string[]> testStringPair;

    [Header("Debug")]
    public bool initializeOnAwake = true;
    public bool useChildObjectInsteadOfSelf = true;

    [Header("Dictionaries")]
    [UseConverter(typeof(DictionaryConverter))]
    public Dictionary<string, ExternalRefComponent> identifiedExternalsPairs;
    [SerializeField]
    private List<ExternalRefComponent> privateIdentificationsPairs;

    [SerializeField]
    private bool __BepInSerializer_BlockSerializationCallbackAction;

    private void Awake()
    {
        if (initializeOnAwake)
        {
            InitializeFullTestData();
        }
        // else
        // BridgeManager.logger.LogInfo("----------- AWAKE CALLED!! --------------");
    }

    private void OnEnable()
    {
        // if (!initializeOnAwake)
        // BridgeManager.logger.LogInfo("----------- OnEnabled CALLED!! --------------");
        initializeOnAwake = false;
    }

    public void OnBeforeSerialize()
    {
        if (__BepInSerializer_BlockSerializationCallbackAction) return;
        BridgeManager.logger.LogInfo("----------- ONBEFORESERIALIZE CALLED!! --------------");
        testStringArray = new string[1];
        testStringArray[0] = testStringPair[0][0];
    }

    public void OnAfterDeserialize()
    {
        if (__BepInSerializer_BlockSerializationCallbackAction) return;
        BridgeManager.logger.LogInfo("----------- ONAFTERDESERIALIZE CALLED!! --------------");
        BridgeManager.logger.LogInfo($"testStringArray: {testStringArray}");
        testStringPair = [testStringArray];
        testStringArray = null;
    }

    // Context menu allows you to trigger this in Editor to verify serialization
    // before the game even runs.
    [ContextMenu("Initialize Full Test Data")]
    public void InitializeFullTestData()
    {
        testStringPair = [["Test"]];
        nonSerializablePayload = new() { someField = 213 };
        refNonSerializablePayload = new() { someField = 213 };
        // Setup External Reference (Ensure component exists)
        var ext = GetComponent<ExternalRefComponent>();
        if (ext == null)
        {
            GameObject objectToAttach = gameObject;
            if (useChildObjectInsteadOfSelf)
            {
                objectToAttach = new GameObject("ChildObject");
                objectToAttach.transform.SetParent(transform, false);
            }
            ext = objectToAttach.AddComponent<ExternalRefComponent>();
        }

        // Dictionary initialization
        identifiedExternalsPairs = new()
        {
          { "DangerousExternal", ext }
        };

        privateIdentificationsPairs = [ext];

        // 0. Ensure Payload Exists
        payload = new BridgePayload();
        payload.externalRef = ext;

        // // 1. Primitives & Boundaries
        payload.textWithSpecialChars = "Hello\nWorld\tWith \"Quotes\" & Symbols";
        payload.emptyString = "";
        payload.nullString = null;
        payload.floatInfinity = float.PositiveInfinity;
        payload.floatNaN = float.NaN;
        payload.intMin = int.MinValue;
        payload.simpleStruct = new() { x = 1, y = 2 };

        // 1.1. Non Serialization
        payload.thisTextMustNotBeSerialized = "Something very bad to be serialized";
        payload.thisNumberMustNotBeSerialized = 9123091;

        // 1.2 Generics
        payload.genericString = new() { Value = "MyValueToBeSerialized" };
        payload.genericInt = new() { Value = 2 };
        payload.genericStruct = new() { Value = new() { x = 3, y = 4 } };
        payload.valueGenericString = new() { Value = "MyValueToBeSerialized" };
        payload.valueGenericInt = new() { Value = 2 };
        payload.valueGenericStruct = new() { Value = new() { Value = 24 } };

        // 1.3 Nullables
        payload.nullableNumber = null;
        payload.nullableStruct = null;
        payload.NullableGenClass = null;
        payload.refNullableGenClass = null;

        // 1.4 Dictionaries
        payload.stringNumPairs = new()
        {
            { "SomeValue", 99 },
            {"Hello World", 2241}
        };
        payload.objectNumPairs = new()
        {
            { this, 99},
            {transform, 1234},
            {gameObject, 23},
            {payload.externalRef, 1232123}
        };
        payload.nestedStringNumPairs = new()
        {
          { [], [] },
          { ["Somevava", "AwesomeString", "This is Important@$&!*()&!*)%!!"], [22, 1224, 99] }
        };
        payload.objectUnityPairs = new(){
            { this, new(23, 99, 124)}
        };
        payload.nestedStructsPairs = new()
        {
          { [new() { Value = new() { x = 3, y = 4 } }], [new() { x = 1, y = 99 }] }
        };

        // 2. Enums
        payload.standardEnum = ComplexityEnum.Complex;
        payload.flagsEnum = StatusFlags.Active | StatusFlags.Invisible;

        // // 3. Unity Value Types
        payload.nonZeroVector = new Vector3(1.1f, 2.2f, 3.3f);
        payload.specificRotation = Quaternion.Euler(45, 90, 180);
        payload.specificLayer = new LayerMask { value = 1 << 0 | 1 << 5 };
        payload.byteColor = new Color32(255, 128, 0, 255);

        // // 4. Unity Reference Types (Internal Data)
        payload.curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
        payload.gradient = new Gradient();
        payload.gradient.SetKeys(
            [new GradientColorKey(Color.red, 0.0f), new GradientColorKey(Color.blue, 1.0f)],
            [new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f)]
        );

        // 5. Unity Object References
        // Setup Self References
        payload.selfGameObject = gameObject;
        payload.selfTransform = transform;
        payload.selfComponent = this;
        payload.typedReference = this;
        payload.gameObjectList = [gameObject];

        // 6. Collections
        payload.intArray = [1, 1, 2, 3, 5, 8];
        payload.intList = [10, 20, 30];
        payload.emptyList = []; // Not null, but 0 count
        payload.explicitlyNullList = null;      // Explicitly null

        // 7. Nested Collections Wrapper
        payload.abstractThreeDimensionalItem = [
            [
                [
                    new WeaponItem { id = "DimensionalSword", damage = 18491 }
                ]
            ]
        ];
        payload.nestedListReferences = [
            null,
            [
                transform,
                this,
                gameObject
            ]
        ];

        // 8. Nested Multi-Dimensional Arrays
        InitializeThreeJaggedArray();
        InitializeTwoDimensionalArray();
        InitializeMultiListArray();
        InitializeMultiListMultiArray();
        InitializeListOfGameObjectArrayArray();
        InitializeTwoDGameObjectArray();

        // 8. Polymorphism
        payload.singlePolyItem = new WeaponItem { id = "Sword", damage = 50 };

        var refWeaponItem = new WeaponItem { id = "Hammer", damage = 125 };
        payload.polyList =
        [
            new WeaponItem { id = "Axe", damage = 75 },
            new PotionItem { id = "Health", healAmount = 25.5f, potionColor = Color.red, simpleStruct = new() { x = 1, y = 2 } },
        ];

        // 9. Object Identity (Shared References)
        // We create ONE object and assign it to TWO fields.
        // The Bridge must maintain that these point to the same memory address after deserialization.
        var sharedItem = new WeaponItem { id = "SharedUnique", damage = 999 };
        payload.sharedRefA = sharedItem;
        payload.sharedRefB = sharedItem;

        // // 10. Circular References (Pure C#)
        // A -> B -> A
        var root = new CircularNode { id = 1 };
        var child = new SecondaryCircularNode { id = 2 };
        root.next = child;
        child.next = root; // The cycle
        payload.circularRoot = root;

        if (!BridgeManager.enableDebugLogs.Value) return;
        BridgeManager.logger.LogInfo($"[BridgeTester] Data Initialized. Shared Ref Identity: {ReferenceEquals(payload.sharedRefA, payload.sharedRefB)}");
    }

    // Test Logic to verify the bridge didn't break anything
    [ContextMenu("Verify Integrity")]
    public void VerifyIntegrity()
    {
        if (!BridgeManager.enableDebugLogs.Value) return;
        if (payload == null)
        {
            BridgeManager.logger.LogError("Payload is null!");
            return;
        }

        // 1. Identity Check
        bool identityPreserved = ReferenceEquals(payload.sharedRefA, payload.sharedRefB);
        BridgeManager.logger.LogInfo($"Identity Preserved (Shared Refs): {identityPreserved}");

        // 2. Circular Check
        bool circlePreserved = false;
        if (payload.circularRoot != null && payload.circularRoot.next != null)
        {
            circlePreserved = ReferenceEquals(payload.circularRoot, payload.circularRoot.next.next);
        }
        BridgeManager.logger.LogInfo($"Circular Reference Preserved: {circlePreserved}");

        // 3. Unity Object Check
        bool unityRefPreserved = payload.selfGameObject == this.gameObject;
        BridgeManager.logger.LogInfo($"Unity Object Reference (Self): {unityRefPreserved}");

        bool extRefPreserved = payload.externalRef != null;
        BridgeManager.logger.LogInfo($"Unity External Reference: {extRefPreserved}");

        // 4. Null vs Empty List Check
        bool nullPreserved = payload.explicitlyNullList == null;
        bool emptyPreserved = payload.emptyList != null && payload.emptyList.Count == 0;
        BridgeManager.logger.LogInfo($"Null List Correct: {nullPreserved} | Empty List Correct: {emptyPreserved}");
    }


    private void InitializeThreeJaggedArray()
    {
        payload.threeJaggedArray = new int[3][][];

        // First dimension
        payload.threeJaggedArray[0] = new int[2][];
        payload.threeJaggedArray[0][0] = new int[] { 1, 2, 3 };
        payload.threeJaggedArray[0][1] = new int[] { 4, 5 };

        // Second dimension
        payload.threeJaggedArray[1] = new int[3][];
        payload.threeJaggedArray[1][0] = new int[] { 6, 7 };
        payload.threeJaggedArray[1][1] = new int[] { 8, 9, 10, 11 };
        payload.threeJaggedArray[1][2] = new int[] { 12 };

        // Third dimension
        payload.threeJaggedArray[2] = new int[1][];
        payload.threeJaggedArray[2][0] = new int[] { 13, 14, 15, 16, 17 };
    }

    private void InitializeTwoDimensionalArray()
    {
        payload.twoDimensionalArray = new int[4][];
        payload.twoDimensionalArray[0] = new int[] { 1, 2, 3 };
        payload.twoDimensionalArray[1] = new int[] { 4, 5, 6, 7 };
        payload.twoDimensionalArray[2] = new int[] { 8, 9 };
        payload.twoDimensionalArray[3] = new int[] { 10, 11, 12, 13, 14 };
    }

    private void InitializeMultiListArray()
    {
        payload.multiListArray = new List<string>[3];

        payload.multiListArray[0] = new List<string> { "Apple", "Banana", "Cherry" };
        payload.multiListArray[1] = new List<string> { "Dog", "Cat" };
        payload.multiListArray[2] = new List<string> { "Red", "Green", "Blue", "Yellow" };
    }

    private void InitializeMultiListMultiArray()
    {
        payload.multiListMultiArray = new List<int[][]>();

        // First jagged array
        int[][] firstArray = new int[2][];
        firstArray[0] = new int[] { 1, 2 };
        firstArray[1] = new int[] { 3, 4, 5 };

        // Second jagged array
        int[][] secondArray = new int[3][];
        secondArray[0] = new int[] { 6, 7, 8 };
        secondArray[1] = new int[] { 9 };
        secondArray[2] = new int[] { 10, 11, 12, 13 };

        payload.multiListMultiArray.Add(firstArray);
        payload.multiListMultiArray.Add(secondArray);
    }

    private void InitializeListOfGameObjectArrayArray()
    {
        payload.listOfGameObjectArrayArray = new List<GameObject[]>[2];

        // First list
        payload.listOfGameObjectArrayArray[0] = new List<GameObject[]>();
        GameObject[] array1 = new GameObject[] { gameObject, gameObject };
        GameObject[] array2 = new GameObject[] { gameObject };
        payload.listOfGameObjectArrayArray[0].Add(array1);
        payload.listOfGameObjectArrayArray[0].Add(array2);

        // Second list
        payload.listOfGameObjectArrayArray[1] = new List<GameObject[]>();
        GameObject[] array3 = new GameObject[] { gameObject, gameObject, gameObject };
        payload.listOfGameObjectArrayArray[1].Add(array3);
    }

    private void InitializeTwoDGameObjectArray()
    {
        payload.twoDGameObjectArray = new GameObject[3][];

        payload.twoDGameObjectArray[0] = new GameObject[] { gameObject, gameObject };
        payload.twoDGameObjectArray[1] = new GameObject[] { gameObject, gameObject, gameObject };
        payload.twoDGameObjectArray[2] = new GameObject[] { gameObject };
    }
}


// Edge Case: Enums and Flags
[Serializable]
public enum ComplexityEnum { Simple = 0, Complex = 100, Invalid = 999 }

[Flags]
[Serializable]
public enum StatusFlags { None = 0, Active = 1 << 0, Paused = 1 << 1, Invisible = 1 << 2 }

// Edge Case: Generic Classes and Structs
[Serializable]
public class GenericClass<T>
{
    public T Value;
}

[Serializable]
public struct GenericStruct<T>
{
    public T Value;
}

// Edge Case: Polymorphism (SerializeReference)

[Serializable]
public abstract class AbstractItem
{
    public string id;
}

[Serializable]
public class WeaponItem : AbstractItem
{
    public int damage;
}

[Serializable]
public class PotionItem : AbstractItem
{
    public float healAmount;
    public Color potionColor;
    public SimpleStruct simpleStruct;
}

// Edge Case: Nested Structs vs Classes
[Serializable]
public struct SimpleStruct
{
    public int x;
    public int y;
}

// Edge Case: Circular Dependencies (Pure C#)
[Serializable]
public class CircularNode
{
    public int id;
    public SecondaryCircularNode next; // Reference to another node
}

[Serializable]
public class SecondaryCircularNode
{
    public int id;
    public CircularNode next; // Reference to another node
}

// MAIN PAYLOADS
public class NonSerializablePayload // Expected to be null by default
{
    public int someField;
}

[Serializable]
public class BridgePayload
{
    [Header("Primitives & Boundaries")]
    public string textWithSpecialChars; // Test: "Test\nNewLines\tTabs"
    public string emptyString;          // Test: ""
    public string nullString;           // Test: null (should remain null)
    public float floatInfinity;         // Test: float.PositiveInfinity
    public float floatNaN;              // Test: float.NaN
    public int intMin;                  // Test: int.MinValue
    public SimpleStruct simpleStruct;

    [Header("Multi-dimensional Arrays")]
    [AllowCollectionNesting]
    public int[][][] threeJaggedArray;
    [AllowCollectionNesting]
    public int[][] twoDimensionalArray;
    [AllowCollectionNesting]
    public List<string>[] multiListArray;
    public List<int[][]> multiListMultiArray;
    [AllowCollectionNesting]
    public List<GameObject[]>[] listOfGameObjectArrayArray;
    [AllowCollectionNesting]
    public GameObject[][] twoDGameObjectArray;


    [Header("Generic Classes")]
    public GenericClass<string> genericString;
    public GenericClass<int> genericInt;
    public GenericClass<SimpleStruct> genericStruct;
    public GenericStruct<string> valueGenericString;
    public GenericStruct<int> valueGenericInt;
    public GenericStruct<GenericClass<int>> valueGenericStruct;

    [Header("Unserialize Support")]
    [NonSerialized]
    public string thisTextMustNotBeSerialized;
    [NonSerialized]
    public int thisNumberMustNotBeSerialized;

    [Header("Nullable Types")]
    public int? nullableNumber;
    public SimpleStruct? nullableStruct;
    [SerializeReference] public GenericClass<int> refNullableGenClass; // Should technically work like nullableStruct
    public GenericClass<int> NullableGenClass; // Should technically work like nullableStruct

    [Header("Dictionaries")]
    public Dictionary<string, int> stringNumPairs;
    [UseConverter(typeof(DictionaryConverter))]
    public Dictionary<UnityEngine.Object, int> objectNumPairs;
    [UseConverter(typeof(DictionaryConverter))]
    public Dictionary<UnityEngine.Object, Vector3> objectUnityPairs;
    [UseConverter(typeof(DictionaryConverter))]
    public Dictionary<List<string>, List<int>> nestedStringNumPairs;
    [AllowCollectionNesting]
    [UseConverter(typeof(DictionaryConverter))]
    public Dictionary<List<GenericStruct<SimpleStruct>>, List<SimpleStruct>> nestedStructsPairs;


    [Header("Enums")]
    public ComplexityEnum standardEnum;
    public StatusFlags flagsEnum;

    [Header("Unity Value Types")]
    public Vector3 nonZeroVector;
    public Quaternion specificRotation;
    public LayerMask specificLayer;
    public Color32 byteColor;           // Test: Precision loss vs Color

    [Header("Unity Reference Types (Internal)")]
    public AnimationCurve curve;        // Complex internal object
    public Gradient gradient;           // Complex internal object

    [Header("Unity Object References (Scene/Assets)")]
    public GameObject selfGameObject;   // Ref to holding GO
    public Transform selfTransform;     // Ref to holding Transform
    public MonoBehaviour selfComponent; // Ref to holding Script
    public SerializationBridgeTester typedReference; // Typed Ref
    public ExternalRefComponent externalRef; // Ref to another script on same or diff GO
    public List<GameObject> gameObjectList;

    [Header("Collections")]
    public int[] intArray;
    public List<int> intList;
    public List<string> emptyList;      // Initialized but count 0
    public List<string> explicitlyNullList; // Actually null

    [Header("Nested Collections Wrapper")]
    [SerializeReference] public List<List<UnityEngine.Object>> nestedListReferences;
    [AllowCollectionNesting]
    public List<List<List<AbstractItem>>> abstractThreeDimensionalItem;

    [Header("Polymorphism ([SerializeReference])")]
    [SerializeReference] public AbstractItem singlePolyItem;
    [SerializeReference] public List<AbstractItem> polyList;

    [Header("Object Identity (Shared Refs)")]
    [SerializeReference] public AbstractItem sharedRefA;
    [SerializeReference] public AbstractItem sharedRefB; // Must point to same instance as A

    [Header("Pure C# Circular Reference")]
    public CircularNode circularRoot;
}

public class ExternalRefComponent : MonoBehaviour
{
    public string verifyIdentity = "I am the external component";
}

public class TestGenericComponent<C> : MonoBehaviour, ISerializationCallbackReceiver
{
    C value;

    public void OnBeforeSerialize()
    {
        BridgeManager.logger.LogInfo("----------- ONBEFORESERIALIZE CALLED!! --------------");
        Debug.Log("Value of C: " + value?.ToString());
    }

    public void OnAfterDeserialize()
    {
        BridgeManager.logger.LogInfo("----------- ONAFTERDESERIALIZE CALLED!! --------------");
        Debug.Log("Value of C: " + value?.ToString());
    }
}

public class ImplementedTestGenericComponent : TestGenericComponent<int>
{

}
public class StringImplementedTestGenericComponent : TestGenericComponent<string>
{

}
#endif