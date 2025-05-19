using System;
using System.Collections.Generic;
using UnityEngine;

public class DragAndSnapWithAnchors : MonoBehaviour
{
    public List<Transform> bottomAnchors;  // ⬅️ 多个锚点
    public List<Vector3Int> regPositions;
    private EditorManager editorManager;
    public float targetY = 0f;             // 吸附高度
    public bool autoSnapOnStart = true;
    private Vector3 startPosition;
    private bool isDragging = false;
    private Camera cam;
    private Plane groundPlane;
    // DEBUG    
    public bool debugDrawAnchors = true;
    public Color anchorColor = Color.yellow;
    public Color snappedColor = Color.green;
    public Color lineColor = Color.cyan;
    
    void OnDrawGizmos()
    {
        if (!debugDrawAnchors || bottomAnchors == null || bottomAnchors.Count == 0)
            return;

        Gizmos.color = lineColor;

        foreach (var anchor in bottomAnchors)
        {
            if (anchor == null) continue;

            Vector3 anchorPos = anchor.position;
            Vector3 snappedPos = new Vector3(
                Mathf.Round(anchor.position.x),
                targetY,
                Mathf.Round(anchor.position.z)
            );

            // 绘制 anchor 点
            Gizmos.color = anchorColor;
            Gizmos.DrawSphere(anchorPos, 0.05f);

            // 绘制吸附目标点
            Gizmos.color = snappedColor;
            Gizmos.DrawCube(snappedPos, Vector3.one * 0.1f);

            // 绘制连线
            Gizmos.color = lineColor;
            Gizmos.DrawLine(anchorPos, snappedPos);
        }
    }

    
    List<Vector3Int> SnapToAnchorsGrid()
    {
        if (bottomAnchors == null || bottomAnchors.Count == 0) return null;

        List<Vector3Int> proposedGridCells = new List<Vector3Int>();
        Vector3 moveDelta = Vector3.zero;

        foreach (var anchor in bottomAnchors)
        {
            Vector3 snapped = new Vector3(
                Mathf.Round(anchor.position.x),
                targetY,
                Mathf.Round(anchor.position.z)
            );

            Vector3 offset = anchor.position - snapped;
            if (moveDelta == Vector3.zero)
                moveDelta = offset;

            Vector3 anchorSnappedWorld = anchor.position - offset;
            Vector3Int gridPos = Vector3Int.RoundToInt(anchorSnappedWorld);
            proposedGridCells.Add(gridPos);
        }

        transform.position -= moveDelta;

        foreach (var cell in proposedGridCells)
        {
            if (GridManager.Instance != null && GridManager.Instance.IsOccupied(cell))
            {
                Debug.LogError("位置被占用，这不合理，重新调整位置");
            }
            GridManager.Instance?.SetOccupied(cell, true);
        }
        return proposedGridCells;
    }

    void Start()
    {
        cam = Camera.main;
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (editorManager == null) editorManager = FindObjectOfType<EditorManager>();
        if (autoSnapOnStart) regPositions = SnapToAnchorsGrid();

    }
    
    
    void Update()
    {
        // 只有编辑模式中可以拖动
        if (editorManager.inEditMode)
        {   
            // 如果鼠标按下 开始拖动
            if (Input.GetMouseButtonDown(0))
            {
                if (IsMouseOverSelf())
                {
                    isDragging = true;
                    startPosition = transform.position;
                }
            }
            // 如果正在拖动
            if (isDragging)
            {
                if (!IsMouseInScreen(Input.mousePosition))
                {
                    CancelDrag();
                    return;
                }

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hit = ray.GetPoint(enter);
                    transform.position = new Vector3(hit.x, transform.position.y, hit.z);
                }
            }
            // 如果结束拖动
            if (Input.GetMouseButtonUp(0) && isDragging)
            {
                Debug.Log(String.Format("尝试放置Cube {0}", gameObject.name));
                isDragging = false;

                // 拟议坐标吸附后所有锚点对应的格子
                List<Vector3Int> proposedGridCells = new List<Vector3Int>();
                Vector3 moveDelta = Vector3.zero;

                foreach (var anchor in bottomAnchors)
                {
                    // 吸附 anchor 到整数网格
                    Vector3 snapped = new Vector3(
                        Mathf.Round(anchor.position.x),
                        targetY,
                        Mathf.Round(anchor.position.z)
                    );

                    // 偏移量 = anchor 当前位置 - snapped
                    Vector3 offset = anchor.position - snapped;

                    // 第一次锚点决定整体偏移
                    if (moveDelta == Vector3.zero)
                        moveDelta = offset;

                    Vector3 anchorSnappedWorld = anchor.position - offset;
                    Vector3Int gridPos = Vector3Int.RoundToInt(anchorSnappedWorld);
                    proposedGridCells.Add(gridPos);
                }

                // 检查是否全部未被占用
                bool canPlace = true;
                foreach (var cell in proposedGridCells)
                {
                    // 排除自己 并且 Grid 中不存在占用
                    if (!regPositions.Contains(cell) && GridManager.Instance.IsOccupied(cell))
                    {
                        Debug.Log(String.Format("发现放置目标被占用 {0}", cell));
                        canPlace = false;
                        break;
                    }
                }

                if (canPlace)
                {
                    // 应用偏移吸附
                    transform.position -= moveDelta;
                    
                    // 注册所有格子
                    foreach (var cell in proposedGridCells)
                        GridManager.Instance.SetOccupied(cell, true);
                    // 删除占用
                    foreach (var cell in regPositions)
                    {
                        GridManager.Instance.SetOccupied(cell, false);
                    }
                    regPositions = proposedGridCells;
                }
                else
                {
                    CancelDrag();
                }
            }
        }
        else // 如果不在编辑模式，但是当前缺在拖动
        {   
            // 取消拖动
            if (isDragging) CancelDrag();
        }
    }

    private void CancelDrag()
    {
        transform.position = startPosition;
        isDragging = false;
    }

    private bool IsMouseOverSelf()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform;
    }

    private bool IsMouseInScreen(Vector3 pos)
    {
        return pos.x >= 0 && pos.x <= Screen.width && pos.y >= 0 && pos.y <= Screen.height;
    }
}
