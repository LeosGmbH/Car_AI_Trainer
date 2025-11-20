using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Reset : MonoBehaviour {

    Transform _tr;
    private Vector3 curPos;
    private InputAction resetAction;

    void Awake()
    {
        curPos = transform.position;
        _tr = transform;
        EnsureResetAction();
    }

    private void EnsureResetAction()
    {
        if (resetAction != null)
        {
            return;
        }

        resetAction = new InputAction("ResetScene", InputActionType.Button);
        resetAction.AddBinding("<Keyboard>/r");
        resetAction.AddBinding("<Gamepad>/buttonSouth");
        resetAction.performed += OnResetPerformed;
    }

    private void OnResetPerformed(InputAction.CallbackContext ctx)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnEnable()
    {
        EnsureResetAction();
        resetAction?.Enable();
    }

    void OnDisable()
    {
        resetAction?.Disable();
    }

    void OnDestroy()
    {
        if (resetAction == null)
        {
            return;
        }

        resetAction.performed -= OnResetPerformed;
        resetAction.Disable();
        resetAction.Dispose();
        resetAction = null;
    }
}
