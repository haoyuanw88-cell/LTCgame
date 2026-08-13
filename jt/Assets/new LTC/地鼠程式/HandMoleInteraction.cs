using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Mediapipe.Tasks.Components.Containers;

public class HandMoleInteraction : MonoBehaviour
{
    public HandLandmarkerRunner handDataSource;
    public MoleManager moleManager;

    [Header("Grab")]
    [Range(-0.1f, 0.15f)]
    public float grabOffset = 0.06f;

    public int grabConfirmFrames = 2;
    public int releaseConfirmFrames = 2;

    [Header("Aim")]
    public Vector2 screenOffset = Vector2.zero;
    public float smoothSpeed = 18f;

    private const int MaxHands = 2;

    private int[] grabFrameCounts = new int[MaxHands];
    private int[] releaseFrameCounts = new int[MaxHands];
    private bool[] stableGrabbings = new bool[MaxHands];
    private bool[] wasStableGrabbings = new bool[MaxHands];

    private Vector2[] smoothedScreenPositions = new Vector2[MaxHands];
    private bool[] hasSmoothedPositions = new bool[MaxHands];

    void Update()
    {
        if (handDataSource == null || moleManager == null) return;
        if (!handDataSource.HasLatestResult) return;

        var result = handDataSource.LatestResult;
        if (result.handLandmarks == null || result.handLandmarks.Count == 0) return;

        int handCount = Mathf.Min(result.handLandmarks.Count, MaxHands);

        for (int handIndex = 0; handIndex < handCount; handIndex++)
        {
            var landmarks = result.handLandmarks[handIndex].landmarks;
            if (landmarks == null || landmarks.Count < 21) continue;

            bool rawGrabbing = DetectGrab(landmarks);

            wasStableGrabbings[handIndex] = stableGrabbings[handIndex];
            stableGrabbings[handIndex] = StabilizeGrab(rawGrabbing, handIndex);

            Vector2 screenPos = GetStableHandCenterScreenPoint(landmarks);
            screenPos += screenOffset;

            if (!hasSmoothedPositions[handIndex])
            {
                smoothedScreenPositions[handIndex] = screenPos;
                hasSmoothedPositions[handIndex] = true;
            }
            else
            {
                smoothedScreenPositions[handIndex] = Vector2.Lerp(
                    smoothedScreenPositions[handIndex],
                    screenPos,
                    Time.deltaTime * smoothSpeed
                );
            }

            bool grabJustStarted = stableGrabbings[handIndex] && !wasStableGrabbings[handIndex];

            if (grabJustStarted)
            {
                moleManager.UpdateFromMediaPipe(smoothedScreenPositions[handIndex], true);
            }

            Ray ray = Camera.main.ScreenPointToRay(smoothedScreenPositions[handIndex]);
            Debug.DrawRay(
                ray.origin,
                ray.direction * 100f,
                stableGrabbings[handIndex] ? Color.red : Color.green,
                0.05f
            );
        }

        ResetMissingHands(handCount);
    }

    void ResetMissingHands(int detectedHandCount)
    {
        for (int i = detectedHandCount; i < MaxHands; i++)
        {
            grabFrameCounts[i] = 0;
            releaseFrameCounts[i] = 0;
            stableGrabbings[i] = false;
            wasStableGrabbings[i] = false;
            hasSmoothedPositions[i] = false;
        }
    }

    Vector2 GetStableHandCenterScreenPoint(System.Collections.Generic.IList<NormalizedLandmark> landmarks)
    {
        float minX = 1f;
        float maxX = 0f;
        float minY = 1f;
        float maxY = 0f;

        for (int i = 0; i < landmarks.Count; i++)
        {
            Vector3 p = ToVector3(landmarks[i]);

            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        float screenX = centerX * Screen.width;
        float screenY = (1f - centerY) * Screen.height;

        return new Vector2(screenX, screenY);
    }

    bool DetectGrab(System.Collections.Generic.IList<NormalizedLandmark> landmarks)
    {
        if (landmarks == null || landmarks.Count < 21) return false;

        Vector3 wrist = ToVector3(landmarks[0]);

        bool indexFolded = IsFingerFolded(ToVector3(landmarks[8]), ToVector3(landmarks[6]), wrist);
        bool middleFolded = IsFingerFolded(ToVector3(landmarks[12]), ToVector3(landmarks[10]), wrist);
        bool ringFolded = IsFingerFolded(ToVector3(landmarks[16]), ToVector3(landmarks[14]), wrist);
        bool pinkyFolded = IsFingerFolded(ToVector3(landmarks[20]), ToVector3(landmarks[18]), wrist);

        int foldedCount = 0;
        if (indexFolded) foldedCount++;
        if (middleFolded) foldedCount++;
        if (ringFolded) foldedCount++;
        if (pinkyFolded) foldedCount++;

        return foldedCount >= 2;
    }

    bool IsFingerFolded(Vector3 tip, Vector3 pip, Vector3 wrist)
    {
        float tipDistance = Vector2.Distance(tip, wrist);
        float pipDistance = Vector2.Distance(pip, wrist);

        return tipDistance < pipDistance + grabOffset;
    }

    bool StabilizeGrab(bool rawGrabbing, int handIndex)
    {
        if (rawGrabbing)
        {
            grabFrameCounts[handIndex]++;
            releaseFrameCounts[handIndex] = 0;

            if (grabFrameCounts[handIndex] >= grabConfirmFrames)
            {
                stableGrabbings[handIndex] = true;
            }
        }
        else
        {
            releaseFrameCounts[handIndex]++;
            grabFrameCounts[handIndex] = 0;

            if (releaseFrameCounts[handIndex] >= releaseConfirmFrames)
            {
                stableGrabbings[handIndex] = false;
            }
        }

        return stableGrabbings[handIndex];
    }

    Vector3 ToVector3(NormalizedLandmark landmark)
    {
        return new Vector3(landmark.x, landmark.y, landmark.z);
    }
}
