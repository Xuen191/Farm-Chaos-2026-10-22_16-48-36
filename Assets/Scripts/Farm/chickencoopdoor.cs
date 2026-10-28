using UnityEngine;
using System.Collections;

public class ChickenCoop : MonoBehaviour
{
    [Header("Door Hinges")]
    public Transform leftDoorHinge;
    public Transform rightDoorHinge;

    [Header("Door Settings")]
    public float openAngle = 100f;
    public float doorSpeed = 3f;
    public float closeDelay = 2f;

    private Quaternion leftClosed;
    private Quaternion rightClosed;
    private Coroutine doorRoutine;

    void Start()
    {
        if (leftDoorHinge != null)
            leftClosed = leftDoorHinge.localRotation;

        if (rightDoorHinge != null)
            rightClosed = rightDoorHinge.localRotation;
    }

    public void ChickenApproached()
    {
        if (doorRoutine != null)
            StopCoroutine(doorRoutine);

        doorRoutine = StartCoroutine(OpenAndClose());
    }

    IEnumerator OpenAndClose()
    {
        yield return RotateDoors(true);

        // Leave the doors open while the chicken passes through.
        yield return new WaitForSeconds(closeDelay);

        yield return RotateDoors(false);

        doorRoutine = null;
    }

    IEnumerator RotateDoors(bool open)
    {
        Quaternion leftTarget = open
            ? leftClosed * Quaternion.Euler(0, openAngle, 0)
            : leftClosed;

        Quaternion rightTarget = open
            ? rightClosed * Quaternion.Euler(0, -openAngle, 0)
            : rightClosed;

        while ((leftDoorHinge != null &&
                Quaternion.Angle(leftDoorHinge.localRotation, leftTarget) > 1f)
            || (rightDoorHinge != null &&
                Quaternion.Angle(rightDoorHinge.localRotation, rightTarget) > 1f))
        {
            if (leftDoorHinge != null)
                leftDoorHinge.localRotation = Quaternion.RotateTowards(
                    leftDoorHinge.localRotation, leftTarget,
                    doorSpeed * 90f * Time.deltaTime);

            if (rightDoorHinge != null)
                rightDoorHinge.localRotation = Quaternion.RotateTowards(
                    rightDoorHinge.localRotation, rightTarget,
                    doorSpeed * 90f * Time.deltaTime);

            yield return null;
        }

        if (leftDoorHinge != null)
            leftDoorHinge.localRotation = leftTarget;

        if (rightDoorHinge != null)
            rightDoorHinge.localRotation = rightTarget;
    }
}
