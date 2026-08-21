using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CenterScreenUIInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]

    [Tooltip("Maximum distance from which world-space UI can be activated.")]
    public float interactionDistance = 5f;

#if ENABLE_INPUT_SYSTEM
    [Tooltip("Button used to activate the UI element at the center of the screen.")]
    public Key interactionKey = Key.E;
#else
    public KeyCode interactionKey = KeyCode.E;
#endif

    private PointerEventData pointerEventData;

    private Button currentButton;


    private void Start()
    {
        if (EventSystem.current == null)
        {
            Debug.LogError(
                "CenterScreenUIInteractor requires an EventSystem in the scene."
            );

            return;
        }

        pointerEventData = new PointerEventData(EventSystem.current);
    }


    private void Update()
    {
        if (EventSystem.current == null)
            return;

        FindButtonAtScreenCenter();

        if (currentButton != null && InteractPressed())
        {
            currentButton.onClick.Invoke();
        }
    }


    private void FindButtonAtScreenCenter()
    {
        // Pretend the mouse pointer is permanently in the center
        // of the player's screen.

        pointerEventData.position = new Vector2(
            Screen.width / 2f,
            Screen.height / 2f
        );

        List<RaycastResult> results = new List<RaycastResult>();

        EventSystem.current.RaycastAll(pointerEventData, results);

        Button newButton = null;

        foreach (RaycastResult result in results)
        {
            // Ignore UI that is too far away.
            if (result.distance > interactionDistance)
                continue;

            Button button = result.gameObject.GetComponentInParent<Button>();

            if (button != null && button.interactable)
            {
                newButton = button;
                break;
            }
        }

        UpdateHighlightedButton(newButton);
    }


    private void UpdateHighlightedButton(Button newButton)
    {
        if (newButton == currentButton)
            return;

        // Stop highlighting the previous button.
        if (currentButton != null)
        {
            ExecuteEvents.Execute(
                currentButton.gameObject,
                pointerEventData,
                ExecuteEvents.pointerExitHandler
            );
        }

        currentButton = newButton;

        // Highlight the button currently in the center of the screen.
        if (currentButton != null)
        {
            ExecuteEvents.Execute(
                currentButton.gameObject,
                pointerEventData,
                ExecuteEvents.pointerEnterHandler
            );
        }
    }


    private bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM

        if (Keyboard.current == null)
            return false;

        return Keyboard.current[interactionKey].wasPressedThisFrame;

#else

        return Input.GetKeyDown(interactionKey);

#endif
    }


    // ============================================================
    // FUTURE VR
    // ============================================================

    /*

    This script is only the DESKTOP pointer implementation.

    In VR, XR Interaction Toolkit would provide the pointer instead:

        XR Controller Ray

        World Space Canvas

             Button

            OnClick()

    This desktop script would simply no longer be needed when the
    XR interaction system is handling the UI.

    */
}