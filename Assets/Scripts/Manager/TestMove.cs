using System;
using UnityEngine;

namespace Manager
{
    public class TestMove : MonoBehaviour
    {
        [SerializeField] private Vector2 targetWorldPosition;
        [SerializeField] private CameraSmoothMove cameraMove;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                cameraMove.MoveTo(targetWorldPosition);
            }
        }
    }
}