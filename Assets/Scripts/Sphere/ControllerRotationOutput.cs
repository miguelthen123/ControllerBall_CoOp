using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ControllerRotationOutput : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private TMP_Text leftControllerText;
    [SerializeField] private TMP_Text rightControllerText;

    private void Update()
    {
        // --- Left Controller ---
        if (OVRInput.IsControllerConnected(OVRInput.Controller.LTouch))
        {
            Quaternion leftRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
            Vector3 leftEuler = leftRot.eulerAngles;

            if (leftControllerText != null)
            {
                leftControllerText.text = $"<b>Left Controller</b>\n" +
                                         $"Pitch (X): {leftEuler.x:F1}°\n" +
                                         $"Yaw (Y): {leftEuler.y:F1}°\n" +
                                         $"Roll (Z): {leftEuler.z:F1}°\n" +
                                         $"Quat: ({leftRot.x:F2}, {leftRot.y:F2}, {leftRot.z:F2}, {leftRot.w:F2})";
            }
        }
        else if (leftControllerText != null)
        {
            leftControllerText.text = "<b>Left Controller</b>\nDisconnected / Tracking Lost";
        }

        // --- Right Controller ---
        if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
        {
            Quaternion rightRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
            Vector3 rightEuler = rightRot.eulerAngles;

            if (rightControllerText != null)
            {
                rightControllerText.text = $"<b>Right Controller</b>\n" +
                                          $"Pitch (X): {rightEuler.x:F1}°\n" +
                                          $"Yaw (Y): {rightEuler.y:F1}°\n" +
                                          $"Roll (Z): {rightEuler.z:F1}°\n" +
                                          $"Quat: ({rightRot.x:F2}, {rightRot.y:F2}, {rightRot.z:F2}, {rightRot.w:F2})";
            }
        }
        else if (rightControllerText != null)
        {
            rightControllerText.text = "<b>Right Controller</b>\nDisconnected / Tracking Lost";
        }
    }
}