using UnityEngine;

public class TrackPoints : MonoBehaviour
{
    [Header("Track Connection Points")]

    public Transform EntryPoint;

    public Transform[] ExitPoints;


    private void OnDrawGizmos()
    {
        // ENTRY
        if (EntryPoint != null)
        {
            Gizmos.color = Color.blue;

            Gizmos.DrawSphere(
                EntryPoint.position,
                0.15f
            );

            Gizmos.DrawLine(
                EntryPoint.position,
                EntryPoint.position +
                EntryPoint.forward * 1.5f
            );
        }


        // EXITS
        if (ExitPoints != null)
        {
            foreach (
                Transform exit
                in ExitPoints
            )
            {
                if (exit == null)
                    continue;


                Gizmos.color = Color.red;

                Gizmos.DrawSphere(
                    exit.position,
                    0.15f
                );

                Gizmos.DrawLine(
                    exit.position,
                    exit.position +
                    exit.forward * 1.5f
                );
            }
        }
    }
}