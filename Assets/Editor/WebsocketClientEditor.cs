using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WebsocketClient))]
class WebsocketClientEditor : Editor
{
    public string destination;
    public string payload;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Message Test", EditorStyles.boldLabel);

        destination = EditorGUILayout.TextField("Destination", destination);
        payload = EditorGUILayout.TextField("Payload", payload);

        if (GUILayout.Button("Send the message or some shit"))
        {
            WebsocketClient.Instance.SendMessage(destination, payload);
        }
    }
}