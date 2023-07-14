// using System;
// using UnityEngine;
//
// namespace ChebsValheimLibrary.Minions
// {
//     public class MinionText : MonoBehaviour
//     {
//         public static float RenderDistance = 5f;
//         public static float VerticalOffset = 2.5f;
//         public static int FontSize = 1;
//
//         private MeshRenderer _meshRenderer;
//         
//         public string Text
//         {
//             get
//             {
//                 if (_textMesh == null) _textMesh = gameObject.AddComponent<TextMesh>();
//                 return _textMesh.text;
//             }
//             set
//             {
//                 if (_textMesh == null) _textMesh = gameObject.AddComponent<TextMesh>();
//                 _textMesh.text = value;
//             }
//         }
//
//         private TextMesh _textMesh;
//
//         private void Awake()
//         {
//             _meshRenderer = gameObject.AddComponent<MeshRenderer>();
//             _textMesh.fontSize = FontSize;
//         }
//
//         private void LateUpdate()
//         {
//             transform.LookAt(Player.m_localPlayer.transform);
//         }
//     }
// }