using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ComputeShaderUtility;

public class SlimeShaderScriptColour : MonoBehaviour
{
    private bool begin = false;
    private bool showInstructs = false;
    private int step;

    const int updateKernel = 0;
    const int diffusedMapKernel = 1;
    const int effectsKernel = 2;

    public ComputeShader computeShader;

    public RenderTexture trailMap;
    public RenderTexture processedTrailMap;
    public RenderTexture postProcessedTrailMap;
    public Texture2D image;

    ComputeBuffer agentBuffer;
    ComputeBuffer colorPointBuffer;

    public int width;
    public int height;
    public int numAgents;
    public float moveSpeed;
    public float turnSpeed;
    public int sensorSize;
    public float sensorAngleDegrees;
    public float sensorOffsetDist;
    public float trailWeight;
    public float diffusionSpeed;
    public float evaporationSpeed;

    public SpawnMode spawnMode;

    public float vertLineThickness;

    private bool recording = false;
    private int recordingNumber;
    public int desiredFPS;
    public int frameInterval;
    private int frameIntervalCounter;

    private int numColors;
    public float colorPointSpeed;
    public float colorPointDecay;
    public Color[] colorArray;


    // Start is called before the first frame update
    void Start()
    {
        numColors = colorArray.Length;
        Debug.Log("numColors = " + numColors);
        // Restrict framerate so my computer doesnt die with large N of particles
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = desiredFPS;

        step = 0;
        // Agent Creation
        Agent[] agents = new Agent[numAgents];
        for (int i=0; i < agents.Length; i++)
        {
            Vector2 centre = new Vector2(width / 2, height / 2);
			Vector2 startPos = Vector2.zero;
			float randomAngle = Random.value * Mathf.PI * 2;
			float angle = 0;
            // Spawn types
            if (spawnMode == SpawnMode.Point)
			{
				startPos = centre;
				angle = randomAngle;
			}
            else if (spawnMode == SpawnMode.InwardCircle)
			{
				startPos = centre + Random.insideUnitCircle * height * 0.5f;
				angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
			}
            else if (spawnMode == SpawnMode.VerticalLines)
            {
                int N = (int)(width / (2 * sensorOffsetDist));
                int groupSize = Mathf.FloorToInt(numAgents / N);
                int agentGroup = Mathf.FloorToInt(i / groupSize);

                float xPos = sensorOffsetDist + agentGroup * (2 * sensorOffsetDist) + Random.Range(-1f, 1f) * vertLineThickness;

                startPos = new Vector2(xPos, 0);
                angle = (float)(Mathf.PI * 0.5);
            }

            agents[i] = new Agent() {position = startPos, angle = angle};
        }
        // ColorPoints creation
        ColorPoint[] colorPoints = new ColorPoint[numColors];
        for(int i=0; i<numColors; i++)
        {
            Color col = colorArray[i];
            colorPoints[i] = new ColorPoint(){position=new Vector2(Random.value*width,Random.value*height),
                                                velocity=Random.onUnitSphere*colorPointSpeed, color=col};
        }

        ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, computeShader, "agents", updateKernel);
        ComputeHelper.CreateAndSetBuffer<ColorPoint> (ref colorPointBuffer, colorPoints, computeShader, "colorPoints", effectsKernel);

        computeShader.SetInt("numAgents", numAgents);
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);

        computeShader.SetFloat("time", Time.time);

        computeShader.SetFloat("moveSpeed", moveSpeed);
        computeShader.SetFloat("turnSpeed", turnSpeed);
        computeShader.SetInt("sensorSize", sensorSize);
        computeShader.SetFloat("sensorAngleDegrees", sensorAngleDegrees);
        computeShader.SetFloat("sensorOffsetDist", sensorOffsetDist);

        computeShader.SetFloat("trailWeight", trailWeight);
        computeShader.SetFloat("diffusionSpeed", diffusionSpeed);
        computeShader.SetFloat("evaporationSpeed", evaporationSpeed);

        computeShader.SetFloat("colorPointDecay", colorPointDecay);
        computeShader.SetInt("numColors", numColors);

        trailMap = new RenderTexture(width, height, 24); // dimensions and buffer depth
        trailMap.enableRandomWrite = true; // give access to random scripts to change
        trailMap.Create(); // constructor doesnt create the actual texture this does

        processedTrailMap = new RenderTexture(width, height, 24); // dimensions and buffer depth
        processedTrailMap.enableRandomWrite = true; // give access to random scripts to change
        processedTrailMap.Create(); // constructor doesnt create the actual texture this does

        postProcessedTrailMap = new RenderTexture(width, height, 24); // dimensions and buffer depth
        postProcessedTrailMap.enableRandomWrite = true; // give access to random scripts to change
        postProcessedTrailMap.Create(); // constructor doesnt create the actual texture this does

        computeShader.SetTexture(updateKernel, "TrailMap", trailMap); // give output texture to CS with an index and string to refference

        computeShader.SetTexture(diffusedMapKernel, "TrailMap", trailMap);
        computeShader.SetTexture(diffusedMapKernel, "ProcessedTrailMap", processedTrailMap);

        computeShader.SetTexture(effectsKernel, "ProcessedTrailMap", processedTrailMap);
        computeShader.SetTexture(effectsKernel, "PostProcessedTrailMap", postProcessedTrailMap);
        computeShader.SetTexture(effectsKernel, "Image", image);

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)){
            begin = !begin;
            if (!showInstructs){showInstructs = true;}
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            Tools.screenshot(postProcessedTrailMap, "Slime/Color");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!recording)
            {
                frameIntervalCounter = 0;
                recordingNumber = Tools.beginRecording("Slime");
            }

            recording = !recording;
        }
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // ASSIGN TEXTURES:
        // trail
        computeShader.SetTexture(updateKernel, "TrailMap", trailMap); // give output texture to CS with an index and string to refference
        // processing
        computeShader.SetTexture(diffusedMapKernel, "TrailMap", trailMap);
        computeShader.SetTexture(diffusedMapKernel, "ProcessedTrailMap", processedTrailMap);
        // post processing
        computeShader.SetTexture(effectsKernel, "ProcessedTrailMap", processedTrailMap);
        computeShader.SetTexture(effectsKernel, "PostProcessedTrailMap", postProcessedTrailMap);
        computeShader.SetTexture(effectsKernel, "Image", image);

        // Variable inputs
        //computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.SetFloat("deltaTime", 0.016667f);
        computeShader.SetFloat("time", Time.time);

        computeShader.SetFloat("moveSpeed", moveSpeed);
        computeShader.SetFloat("turnSpeed", turnSpeed);
        computeShader.SetInt("sensorSize", sensorSize);
        computeShader.SetFloat("sensorAngleDegrees", sensorAngleDegrees);
        computeShader.SetFloat("sensorOffsetDist", sensorOffsetDist);

        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);

        computeShader.SetFloat("trailWeight", trailWeight);
        computeShader.SetFloat("diffusionSpeed", diffusionSpeed);
        computeShader.SetFloat("evaporationSpeed", evaporationSpeed);

        computeShader.SetFloat("colorPointDecay", colorPointDecay);
        computeShader.SetInt("numColors", numColors);


        if (begin)
        {
            // Dispatches
            ComputeHelper.Dispatch(computeShader, numAgents, 1, 1, kernelIndex: updateKernel); // Does the same but neater
            ComputeHelper.Dispatch(computeShader, width, height, 1, kernelIndex: diffusedMapKernel);

            // Necessary to swap the trail for the processed to keep processing more the next time
            ComputeHelper.CopyRenderTexture(processedTrailMap, trailMap);

            // Effects dispatch
            ComputeHelper.Dispatch(computeShader, width, height, 1, kernelIndex: effectsKernel);

            if (recording)
            {    
                if (frameIntervalCounter >= frameInterval){frameIntervalCounter = 0;}
                if (frameIntervalCounter == 0){
                    Tools.addFrame(postProcessedTrailMap, "Slime", recordingNumber);
                }

                frameIntervalCounter++;
            }
        }

        Graphics.Blit(postProcessedTrailMap, dest);
        step++;
    }

    void OnGUI()
    {
        string fps = "FPS: " + (int)(1f / Time.unscaledDeltaTime);
        GUI.Label(new Rect(10, 10, 100, 50), fps);
        if (!showInstructs){
            GUI.Label(new Rect(480, 344, 540, 40), "Begin and pause with SPACE. Return to menu with ESC.");
        }
    }

    void OnDisable()
    {
        ComputeHelper.Release(agentBuffer);
        ComputeHelper.Release(colorPointBuffer);
    }

    public struct Agent
    {
        public Vector2 position;
        public float angle;
    }

    public struct ColorPoint
    {
        public Vector2 position;
        public Vector2 velocity;
        public Color color;
    }

    public enum SpawnMode
    {
        Point,
        InwardCircle,
        VerticalLines
    }
}