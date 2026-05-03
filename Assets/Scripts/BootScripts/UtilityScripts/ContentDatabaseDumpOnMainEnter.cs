using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ContentDatabaseDumpOnMainEnter : MonoBehaviour
{
    [Header("Run Option")]
    [SerializeField]
    private bool dumpOnStart = true;

    [SerializeField]
    private bool runOnlyOncePerPlay = true;

    private static bool s_hasDumpedThisPlay;

    private void Start()
    {
        if (!dumpOnStart)
        {
            return;
        }

        if (runOnlyOncePerPlay && s_hasDumpedThisPlay)
        {
            return;
        }

        DumpNow();
    }

    [ContextMenu("Dump Content DB Now")]
    public void DumpNow()
    {
        ContentDatabaseProvider provider = ContentDatabaseProvider.Instance;

        if (provider == null)
        {
            Debug.LogError(
                "[ContentDatabaseDumpOnMainEnter] ContentDatabaseProvider.Instance is null. "
                    + "Boot 씬을 거치지 않고 Main 씬만 바로 실행했을 가능성이 큼.",
                this
            );
            return;
        }

        ContentDatabaseSO database = provider.Database;

        if (database == null)
        {
            Debug.LogError(
                "[ContentDatabaseDumpOnMainEnter] provider.Database 가 null임. "
                    + "AFC 오브젝트의 ContentDatabaseProvider.contentDatabase 할당을 확인해.",
                this
            );
            return;
        }

        StringBuilder sb = new StringBuilder(16384);

        sb.AppendLine("==================================================");
        sb.AppendLine("CONTENT DB FULL DUMP START");
        sb.AppendLine("==================================================");
        sb.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Database Asset: {database.name} ({database.GetType().Name})");
        sb.AppendLine();

        AppendScriptableObjectDump(sb, "ContentDatabaseSO", database);
        AppendScriptableObjectDump(sb, "BalanceSO", provider.Balance);

        AppendScriptableObjectListDump(sb, "GladiatorClasses", provider.GladiatorClasses);
        AppendScriptableObjectListDump(sb, "Weapons", provider.Weapons);
        AppendScriptableObjectListDump(sb, "Traits", provider.Traits);
        AppendScriptableObjectListDump(sb, "Synergies", provider.Synergies);
        AppendScriptableObjectListDump(sb, "Artifacts", provider.Artifacts);
        AppendScriptableObjectListDump(sb, "Personalities", provider.Personalities);

        sb.AppendLine("==================================================");
        sb.AppendLine("CONTENT DB FULL DUMP END");
        sb.AppendLine("==================================================");

        Debug.Log(sb.ToString(), this);

        if (runOnlyOncePerPlay)
        {
            s_hasDumpedThisPlay = true;
        }
    }

    private static void AppendScriptableObjectListDump<T>(StringBuilder sb, string sectionName, IReadOnlyList<T> list)
        where T : ScriptableObject
    {
        sb.AppendLine($"[{sectionName}]");

        if (list == null)
        {
            sb.AppendLine("  null");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"  Count = {list.Count}");

        for (int i = 0; i < list.Count; i++)
        {
            T item = list[i];

            AppendScriptableObjectDump(sb, $"{sectionName}[{i}]", item, "  ");
        }

        sb.AppendLine();
    }

    private static void AppendScriptableObjectDump(
        StringBuilder sb,
        string label,
        ScriptableObject so,
        string indent = ""
    )
    {
        sb.AppendLine($"{indent}[{label}]");

        if (so == null)
        {
            sb.AppendLine($"{indent}  null");
            return;
        }

        sb.AppendLine($"{indent}  AssetName = {so.name}");
        sb.AppendLine($"{indent}  Type = {so.GetType().Name}");

        FieldInfo[] fields = GetSerializableFields(so.GetType());

        if (fields.Length == 0)
        {
            sb.AppendLine($"{indent}  (serializable field 없음)");
            return;
        }

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            object value = field.GetValue(so);

            AppendValue(sb, $"{indent}  ", field.Name, value);
        }
    }

    private static FieldInfo[] GetSerializableFields(Type type)
    {
        FieldInfo[] rawFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        List<FieldInfo> result = new List<FieldInfo>(rawFields.Length);

        for (int i = 0; i < rawFields.Length; i++)
        {
            FieldInfo field = rawFields[i];

            if (field.IsStatic)
            {
                continue;
            }

            bool isSerializableField = field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));

            if (!isSerializableField)
            {
                continue;
            }

            result.Add(field);
        }

        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result.ToArray();
    }

    private static void AppendValue(StringBuilder sb, string indent, string fieldName, object value)
    {
        if (value == null)
        {
            sb.AppendLine($"{indent}{fieldName} = null");
            return;
        }

        if (value is string stringValue)
        {
            sb.AppendLine($"{indent}{fieldName} = \"{stringValue}\"");
            return;
        }

        if (value is ScriptableObject soValue)
        {
            sb.AppendLine($"{indent}{fieldName} = {soValue.name} ({soValue.GetType().Name})");
            return;
        }

        if (value is UnityEngine.Object unityObject)
        {
            sb.AppendLine($"{indent}{fieldName} = {unityObject.name} ({unityObject.GetType().Name})");
            return;
        }

        if (value is IList list)
        {
            AppendListValue(sb, indent, fieldName, list);
            return;
        }

        sb.AppendLine($"{indent}{fieldName} = {value}");
    }

    private static void AppendListValue(StringBuilder sb, string indent, string fieldName, IList list)
    {
        if (list == null)
        {
            sb.AppendLine($"{indent}{fieldName} = null");
            return;
        }

        sb.AppendLine($"{indent}{fieldName} (Count = {list.Count})");

        for (int i = 0; i < list.Count; i++)
        {
            object element = list[i];
            string elementText = FormatSingleValue(element);
            sb.AppendLine($"{indent}  [{i}] = {elementText}");
        }
    }

    private static string FormatSingleValue(object value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is string stringValue)
        {
            return $"\"{stringValue}\"";
        }

        if (value is ScriptableObject soValue)
        {
            return $"{soValue.name} ({soValue.GetType().Name})";
        }

        if (value is UnityEngine.Object unityObject)
        {
            return $"{unityObject.name} ({unityObject.GetType().Name})";
        }

        return value.ToString();
    }
}
