using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndpointHint : MonoBehaviour
{
    public float duration = 3f;
    public float minYOffset = 0.2f;
    public float maxYOffset = 0.5f;
    public float moveSpeed = 2f;
    public string labelText = "(終點)";
    public Color labelColor = Color.red;
    public Color blueLabelColor = Color.blue;
    public Color redLabelColor = Color.red;
    public Vector3 labelLocalOffset = new Vector3(0f, 0.45f, 0f);
    public bool destroyAfterHide;

    private Transform endpoint;
    private Coroutine animationCoroutine;
    private TextMesh label;

    public static void Play(GameObject hintObject, Blockin endpointPipe)
    {
        if (hintObject == null || endpointPipe == null)
        {
            return;
        }

        EndpointHint hint = hintObject.GetComponent<EndpointHint>();
        if (hint == null)
        {
            hint = hintObject.AddComponent<EndpointHint>();
        }

        hint.Play(endpointPipe.transform, endpointPipe.pipeColor);
    }

    public static void PlayAll(GameObject hintTemplate, IReadOnlyList<Blockin> endpointPipes)
    {
        if (hintTemplate == null || endpointPipes == null)
        {
            return;
        }

        bool templateWasActive = hintTemplate.activeSelf;

        for (int i = 0; i < endpointPipes.Count; i++)
        {
            Blockin endpointPipe = endpointPipes[i];
            if (endpointPipe == null)
            {
                continue;
            }

            GameObject hintObject = i == 0
                ? hintTemplate
                : Instantiate(hintTemplate, hintTemplate.transform.parent);

            hintObject.name = i == 0 ? hintTemplate.name : hintTemplate.name + "_" + (i + 1);
            hintObject.SetActive(templateWasActive || i == 0);

            EndpointHint hint = hintObject.GetComponent<EndpointHint>();
            if (hint == null)
            {
                hint = hintObject.AddComponent<EndpointHint>();
            }

            hint.destroyAfterHide = i != 0;
            hint.Play(endpointPipe.transform, endpointPipe.pipeColor);
        }
    }

    public void Play(Transform endpointTransform)
    {
        Play(endpointTransform, PipeFlowColor.Red);
    }

    public void Play(Transform endpointTransform, PipeFlowColor endpointColor)
    {
        if (endpointTransform == null)
        {
            return;
        }

        endpoint = endpointTransform;
        ApplyEndpointColor(endpointColor);
        gameObject.SetActive(true);
        EnsureLabel();

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float wave = (Mathf.Sin(elapsed * Mathf.PI * 2f * moveSpeed) + 1f) * 0.5f;
            float yOffset = Mathf.Lerp(minYOffset, maxYOffset, wave);
            transform.position = endpoint.position + Vector3.up * yOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        animationCoroutine = null;
        if (destroyAfterHide)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ApplyEndpointColor(PipeFlowColor endpointColor)
    {
        PipeFlowColor normalizedColor = PipeLevelUtility.NormalizeFlowColor(endpointColor);
        if (normalizedColor == PipeFlowColor.Blue)
        {
            labelText = "(藍色終點)";
            labelColor = blueLabelColor;
        }
        else
        {
            labelText = "(紅色終點)";
            labelColor = redLabelColor;
        }
    }

    private void EnsureLabel()
    {
        if (label == null)
        {
            Transform existingLabel = transform.Find("EndpointLabel");
            if (existingLabel != null)
            {
                label = existingLabel.GetComponent<TextMesh>();
            }
        }

        if (label == null)
        {
            GameObject labelObject = new GameObject("EndpointLabel");
            labelObject.transform.SetParent(transform, false);
            label = labelObject.AddComponent<TextMesh>();
        }

        label.text = labelText;
        label.color = labelColor;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 80;
        label.characterSize = 0.035f;
        label.transform.localPosition = labelLocalOffset;
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one;

        SpriteRenderer parentRenderer = GetComponent<SpriteRenderer>();
        MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
        if (parentRenderer != null && labelRenderer != null)
        {
            labelRenderer.sortingLayerID = parentRenderer.sortingLayerID;
            labelRenderer.sortingOrder = parentRenderer.sortingOrder + 1;
        }
    }
}
