﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


/// <summary>
/// UGUIMODEL组件，用来展示3D人物形象
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(EmptyRaycast))]
public class UGUIModel : UIBehaviour, IPointerClickHandler, IDragHandler, IPointerDownHandler, IPointerUpHandler
{

    #region 属性字段
    /// <summary>
    /// 模型的LayerName
    /// </summary>
    private const string UIModelLayerTag = "UI_Model";

    [SerializeField]
    [Tooltip("模型的缩放")]
    private Vector3 scale = Vector3.one;
    [SerializeField]
    [Tooltip("模型的X坐标")]
    private float positionX = 0.0f;
    [SerializeField]
    [Tooltip("模型的Z坐标")]
    private float positionZ = 0.0f;

    [SerializeField]
    [Tooltip("模型的X轴偏移量")]
    private float modelOffsetX = 0.0f;
    [SerializeField]
    [Tooltip("模型的Z轴偏移量")]
    private float modelOffsetZ = 0.0f;

    [SerializeField]
    [Tooltip("相机距离模型的距离")]
    private float cameraDistance = 3.0f;

    [SerializeField]
    [Tooltip("相机相对模型高度")]
    public float cameraHeightOffset = 0.0f;

    [SerializeField]
    [Tooltip("相机视野范围")]
    private int fieldOfView = 60;

    [SerializeField]
    [Tooltip("相机裁剪距离")]
    private int farClipPlane = 20;

    [SerializeField]
    [Tooltip("相机深度")]
    private float modelCameraDepth = 1;

    [Tooltip("相机X轴旋转参数")]
    [SerializeField]
    private float cameraPitch = 0.0f;

    [Tooltip("相机Y轴旋转参数")]
    [SerializeField]
    private float cameraYaw = 90;

    [SerializeField]
    [Tooltip("模型是否可以旋转")]
    private bool enableRotate = true;

    private GameObject root;
    private Camera uiCamera;
    private Camera modelCamera;
    private RectTransform rectTransform;
    private Transform modelRoot;
    private static Vector3 curPos = Vector3.zero;
    private Transform model;
    private int frameCount = 1;
    private bool isInEditor = false;

    private Vector3 tempRelaPosition = Vector3.zero;
    private Vector3 tempOffset = Vector3.zero;
    private Vector3[] screenCorners = new Vector3[4];

    //提前申请RaycatHit数组，避免频繁申请产生GC
    private RaycastHit[] hitInfos = new RaycastHit[20];

    public Transform Model
    {
        get { return model; }
        set
        {
            model = value;
            model.SetParent(modelRoot);
            frameCount = 1;
        }
    }

    public float ModelCameraDepth
    {
        get { return ModelCameraDepth; }
        set
        {
            modelCameraDepth = value;
            modelCamera.depth = modelCameraDepth;
        }
    }

    /// <summary>
    /// 模型点击以后的回调函数
    /// </summary>
    public Action<string> onModelClick;

    #endregion

    protected override void Awake()
    {
        base.Awake();
        uiCamera = GUIHelper.GetUICamera();
        rectTransform = this.GetComponent<RectTransform>();
        root = new GameObject("uguiModel");
        root.transform.position = curPos;
        curPos += new Vector3(200, 0, 0);

        modelCamera = new GameObject("modelCamera", typeof(Camera)).GetComponent<Camera>();
        modelCameraDepth = modelCamera.depth + 1.0f;
        modelCamera.cullingMask = LayerMask.GetMask(UIModelLayerTag);
        modelCamera.clearFlags = CameraClearFlags.Nothing;
        modelCamera.fieldOfView = fieldOfView;
        modelCamera.farClipPlane = farClipPlane;
        modelCamera.transform.SetParent(root.transform);

        modelRoot = new GameObject("model_root").transform;
        modelRoot.transform.SetParent(root.transform);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (null != modelCamera)
        {
            modelCamera.enabled = true;
        }
        UpdateCameraEffect();
    }



    public void OnDrag(PointerEventData eventData)
    {
        if (enableRotate)
        {
            cameraYaw -= eventData.delta.x;
        }
    }

    private void OnClickModel()
    {
        //每次使用前清空结构体数组
        System.Array.Clear(hitInfos, 0, hitInfos.Length);
        Ray ray = modelCamera.ScreenPointToRay(Input.mousePosition);
        Physics.RaycastNonAlloc(ray, hitInfos, 100.0f, LayerMask.GetMask(UIModelLayerTag));
        for (int i = 0; i < hitInfos.Length; i++)
        {
            var hit = hitInfos[i];
            var collider = hit.collider;
            if (null != collider)
            {
                var name = collider.name;
                if ("model_head" == name || "model_body" == "name" || "model_foot" == name)
                {
                    if (null != onModelClick)
                    {
                        onModelClick(name);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 设置模型的整体的Layer(包括子节点)
    /// </summary>
    /// <param name="modelTrans"></param>
    private void SetModelLayer(Transform modelTrans)
    {
        foreach (var trans in modelTrans.GetComponentsInChildren<Transform>())
        {
            trans.gameObject.layer = LayerMask.NameToLayer(UIModelLayerTag);
        }
    }

    private void Update()
    {
        if (null == modelCamera)
        {
            Debug.LogError("Error No ModelCamera!");
            return;
        }
        if (model)
        {
            model.localPosition = Vector3.zero;
            if (frameCount > 0)
            {
                SetModelLayer(model);
                frameCount--;
            }
        }
        //计算x,y,z的单位向量
        float y = Mathf.Sin(cameraPitch * Mathf.Deg2Rad);
        float x = Mathf.Cos(cameraYaw * Mathf.Deg2Rad);
        float z = Mathf.Sin(cameraYaw * Mathf.Deg2Rad);
        //对单位向量进行放大，拿到真实的世界坐标
        float radius = Mathf.Cos(cameraPitch * Mathf.Deg2Rad) * cameraDistance;
        tempRelaPosition.Set(x * radius, y * cameraDistance, z * radius);
        tempOffset.Set(0, cameraHeightOffset, 0);
        modelCamera.transform.position = modelRoot.position + tempRelaPosition + tempOffset;
        Vector3 tempForward = modelRoot.position + tempOffset - modelCamera.transform.position;
        if (tempForward.sqrMagnitude >= 0)
        {
            modelCamera.transform.forward = tempForward;
        }

        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;
        rectTransform.GetWorldCorners(screenCorners);

        //适配UI
        //left botton corner of screen
        var screen_lb = uiCamera.WorldToScreenPoint(screenCorners[0]);
        //right top corner of screen
        var screen_rt = uiCamera.WorldToScreenPoint(screenCorners[2]);
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;

        float w = (screen_rt - screen_lb).x / screenWidth;
        float h = (screen_rt - screen_lb).y / screenHeight;
        modelCamera.rect = new Rect(screen_lb.x / screenWidth, screen_lb.y / screenHeight, w, h);

    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (null != modelCamera)
        {
            modelCamera.enabled = false;
        }
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (null != root)
        {
            Destroy(root);
            root = null;
        }
    }

    public void SetCameraEffect(bool isEnable)
    {
        UpdateCameraEffect();
    }

    private void UpdateCameraEffect()
    {

    }

    public void ImportSetting(string settingName = "")
    {
        //TODO:读取序列化的配置文件，如果没有找到配置就使用默认配置
        if (string.IsNullOrEmpty(settingName))
        {
            DefaultSetting();
        }
    }

    private void DefaultSetting()
    {
        cameraPitch = 0;
        cameraYaw = 90;
        cameraDistance = 7;
        cameraHeightOffset = 0.47f;
        modelCameraDepth = 6;
        positionX = 0;
        positionZ = 0;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClickModel();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }


    #region RunInEditor
#if UNITY_EDITOR
    [LuaInterface.NoToLua]
    [ExecuteInEditMode]
    public void UpdateInEditor()
    {
        if (null == modelCamera)
        {
            Debug.LogError("Error No ModelCamera!");
            return;
        }
        if (model)
        {
            //编辑器模式下直接设置模型的偏移量
            model.localPosition = new Vector3(positionX + modelOffsetX, 0, positionZ + modelOffsetZ);
            if (frameCount > 0)
            {
                SetModelLayer(model);
                frameCount--;
            }
        }
        //计算x,y,z的单位向量
        float y = Mathf.Sin(cameraPitch * Mathf.Deg2Rad);
        float x = Mathf.Cos(cameraYaw * Mathf.Deg2Rad);
        float z = Mathf.Sin(cameraYaw * Mathf.Deg2Rad);
        //对单位向量进行放大，拿到真实的世界坐标
        float radius = Mathf.Cos(cameraPitch * Mathf.Deg2Rad) * cameraDistance;
        tempRelaPosition.Set(x * radius, y * cameraDistance, z * radius);
        tempOffset.Set(0, cameraHeightOffset, 0);
        modelCamera.transform.position = modelRoot.position + tempRelaPosition + tempOffset;
        Vector3 tempForward = modelRoot.position + tempOffset - modelCamera.transform.position;
        if (tempForward.sqrMagnitude >= 0)
        {
            modelCamera.transform.forward = tempForward;
        }

        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;
        rectTransform.GetWorldCorners(screenCorners);

        //适配UI
        //left botton corner of screen
        var screen_lb = uiCamera.WorldToScreenPoint(screenCorners[0]);
        //right top corner of screen
        var screen_rt = uiCamera.WorldToScreenPoint(screenCorners[2]);
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;

        float w = (screen_rt - screen_lb).x / screenWidth;
        float h = (screen_rt - screen_lb).y / screenHeight;
        modelCamera.rect = new Rect(screen_lb.x / screenWidth, screen_lb.y / screenHeight, w, h);
    }

    [LuaInterface.NoToLua]
    [ExecuteInEditMode]
    public void ImportModelInEditor()
    {
        if (null != modelRoot)
        {
            if (modelRoot.childCount > 0)
            {
                model = modelRoot.GetChild(0);
            }
            else
            {
                model = null;
            }
            ImportSetting();
        }
    }


    [LuaInterface.NoToLua]
    [ExecuteInEditMode]
    public void InitInEditor(Camera camera)
    {
        if (!gameObject.activeSelf)
        {
            return;
        }
        uiCamera = camera;
        rectTransform = transform as RectTransform;
        root = GameObject.Find("uguimodel_in_editor");
        if (null == root)
        {
            root = new GameObject("uguimodel_in_editor");
            root.transform.localRotation = Quaternion.identity;
        }
        root.transform.position = curPos;
        curPos += new Vector3(200, 0, 0);

        var modelCameraObj = GameObject.Find("model_camera_in_editor");
        if (null == modelCameraObj)
        {
            modelCamera = new GameObject("model_camera_in_editor", typeof(Camera)).GetComponent<Camera>();
        }
        modelCameraDepth = modelCamera.depth + 1.0f;
        modelCamera.cullingMask = LayerMask.GetMask(UIModelLayerTag);
        modelCamera.clearFlags = CameraClearFlags.Nothing;
        modelCamera.fieldOfView = fieldOfView;
        modelCamera.farClipPlane = farClipPlane;
        modelCamera.transform.SetParent(root.transform);

        var cameraModelRootObj = GameObject.Find("model_root_in_editor");
        if (null == cameraModelRootObj)
        {
            modelRoot = new GameObject("model_root_in_editor").transform;
        }
        modelRoot.transform.SetParent(root.transform);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;

        if (modelRoot.childCount > 0)
        {
            model = modelRoot.GetChild(0);
        }
        else
        {
            model = null;
        }
        isInEditor = true;
    }

    [LuaInterface.NoToLua]
    [ExecuteInEditMode]
    public void SaveSetting()
    {

    }
#endif
    #endregion
}