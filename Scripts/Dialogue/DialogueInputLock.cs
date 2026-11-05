using UnityEngine;
using StarterAssets;
using Yarn.Unity;

public class DialogueInputLock : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private StarterAssetsInputs playerInputs;

    private void OnEnable()
    {
        dialogueRunner.onDialogueStart.AddListener(LockPlayer);
        dialogueRunner.onDialogueComplete.AddListener(UnlockPlayer);
    }

    private void OnDisable()
    {
        dialogueRunner.onDialogueStart.RemoveListener(LockPlayer);
        dialogueRunner.onDialogueComplete.RemoveListener(UnlockPlayer);
    }

    private void LockPlayer() => playerInputs.SetLocked(true);
    private void UnlockPlayer() => playerInputs.SetLocked(false);
}