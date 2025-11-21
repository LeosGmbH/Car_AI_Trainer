using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ForkController : MonoBehaviour
{

    public Transform fork;
    public Transform mast;
    public float speedTranslate; //Platform travel speed
    public Vector3 maxY; //The maximum height of the platform
    public Vector3 minY; //The minimum height of the platform
    public Vector3 maxYmast; //The maximum height of the mast
    public Vector3 minYmast; //The minimum height of the mast

    private bool mastMoveTrue = false; //Activate or deactivate the movement of the mast
    private float externalForkInput;
    private bool hasExternalInput;

    public void SetForkInput(float forkInput)
    {
        externalForkInput = Mathf.Clamp(forkInput, -1f, 1f);
        hasExternalInput = true;
    }

    private float GetKeyboardForkInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        if (keyboard.upArrowKey.isPressed && !keyboard.downArrowKey.isPressed)
        {
            return 1f;
        }

        if (keyboard.downArrowKey.isPressed && !keyboard.upArrowKey.isPressed)
        {
            return -1f;
        }

        return 0f;
    }

    private float GetForkInput()
    {
        if (hasExternalInput)
        {
            return externalForkInput;
        }

        return GetKeyboardForkInput();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        UpdateMastState();
        ApplyForkMovement();
    }

    private void UpdateMastState()
    {
        if (fork.transform.localPosition.y >= maxYmast.y && fork.transform.localPosition.y < maxY.y)
        {
            mastMoveTrue = true;
        }
        else
        {
            mastMoveTrue = false;
        }

        if (fork.transform.localPosition.y <= maxYmast.y)
        {
            mastMoveTrue = false;
        }
    }

    private void ApplyForkMovement()
    {
        float forkInput = GetForkInput();
        if (Mathf.Approximately(forkInput, 0f))
        {
            return;
        }

        if (forkInput > 0f)
        {
            MoveForkTowards(maxY, forkInput, maxYmast);
        }
        else if (forkInput < 0f)
        {
            MoveForkTowards(minY, -forkInput, minYmast);
        }
    }

    private void MoveForkTowards(Vector3 target, float intensity, Vector3 mastTarget)
    {
        fork.transform.localPosition = Vector3.MoveTowards(fork.transform.localPosition, target, speedTranslate * intensity * Time.deltaTime);

        if (mastMoveTrue)
        {
            mast.transform.localPosition = Vector3.MoveTowards(mast.transform.localPosition, mastTarget, speedTranslate * intensity * Time.deltaTime);
        }
    }
}
