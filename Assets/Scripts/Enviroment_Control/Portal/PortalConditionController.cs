using UnityEngine;

/// <summary>
/// Keeps conditional portals closed until game logic reports that a condition
/// has been satisfied. Number keys are only a temporary test input.
/// </summary>
public sealed class PortalConditionController : MonoBehaviour
{
    [SerializeField] private Portal2D conditionOnePortal;
    [SerializeField] private Portal2D conditionTwoPortal;
    [SerializeField] private bool exclusiveSelection = true;
    [SerializeField] private bool enableNumberKeyTesting = true;

    private bool conditionSelected;

    private void Start()
    {
        conditionOnePortal?.ClosePortal();
        conditionTwoPortal?.ClosePortal();
    }

    private void Update()
    {
        if (!enableNumberKeyTesting)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            SatisfyCondition1();
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            SatisfyCondition2();
    }

    public void SatisfyCondition1()
    {
        SatisfyCondition(1);
    }

    public void SatisfyCondition2()
    {
        SatisfyCondition(2);
    }

    public void SatisfyCondition(int conditionNumber)
    {
        if (exclusiveSelection && conditionSelected)
            return;

        Portal2D selectedPortal;
        switch (conditionNumber)
        {
            case 1:
                selectedPortal = conditionOnePortal;
                break;
            case 2:
                selectedPortal = conditionTwoPortal;
                break;
            default:
                Debug.LogWarning($"没有编号为 {conditionNumber} 的传送门条件。", this);
                return;
        }

        if (selectedPortal == null)
        {
            Debug.LogWarning($"条件 {conditionNumber} 没有配置传送门。", this);
            return;
        }

        if (exclusiveSelection)
        {
            conditionOnePortal?.ClosePortal();
            conditionTwoPortal?.ClosePortal();
            conditionSelected = true;
        }

        selectedPortal.OpenPortal();
    }
}
