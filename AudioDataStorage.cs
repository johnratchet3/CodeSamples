using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NAudio.Dsp;
using System.IO; //used for waveMemoryStream!
using System;
using DSPLib; //added library for discrete



//using this bad boy as a central place to store all important audio data. 

/*
 * Heyo. If you're reading this, thanks for taking interest in my work. Apologies for this being an especially monolithic mess,
 * but it was written with just me in mind and for a series of experiments, so it's due to be simplified, trimmed, sorted out,
 * etc. Having said that, there's some cool stuff going on; the long and short of it is that this class stores all the interesting
 * data out of an audio sample, mainly attempting to calculate where beats occur (using offset detection with fast fourier transform).
 * It could save a lot of memory by re-using arrays rather than making new ones, but the separate arrays often get used in debug
 * visualisation for study, so I kept them separated. Performance can especially be improved by async tasks to utilise multi-threading;
 * although there is a long sequence of tasks, most of the tasks end up looping through subsets of arrays (namely, data of a particular 
 * frequency range), so each of those could be done in parallel once separated. Given the load time is about 2 seconds per minute of 
 * audio, and most of this is in the raw mp3 to wav conversion (cannot be easily threaded), I'm in no rush to update it just yet.
 * 
 * You can check out the results of this class here; https://youtu.be/0-3fWyLrJPM
 */
public class AudioDataStorage : MonoBehaviour {
    private AudioSource localAudioSource;
    public GameController gc;

    //IF THESE ARE SERIALISED, THEY SHOW UP IN THE EDITOR, AND LOWER OUR FPS TO SPF. LIKE 30s PER FRAME.
    //I will later make some of these private where possible, but the JVisualiser method needs access (and will probably be the only script Get'ing from here).
    [System.NonSerialized]
    public float[] LC, RC, CC; //Left, Right, and Combined channels.
    [System.NonSerialized]
    public float[] LCMagnitude, RCMagnitude, CCMagnitude; //^but normalised to all positives. Combined = simple L-R (resulting in +1 to -1).
    [System.NonSerialized] //these CAN be serialised, but expect major slowdowns. Pause game before parsing.
    public float[] xLCMagnitude, xRCMagnitude, xCCMagnitude; //reduced detail arrays; averaged in discrete chunks (downsampled/reduced).
    [System.NonSerialized]
    public float[] xrLC, xrRC; //reduced, normalized, rolling average magnitudes for left and right. used for determining which side to spawn stuff on mainly. also visuals.
    [System.NonSerialized]
    public float[] LCL1, LCL2, LCL3, LCL4, LCL5, LCL6; //left channel lowpass. testing.
    [System.NonSerialized]
    public float[] xLCL1, xLCL2, xLCL3, xLCL4, xLCL5, xLCL6; //left channel lowpass. testing.
    [System.NonSerialized]
    public float[] beatTimes1, beatTimes2, beatTimes3, beatTimes4, beatTimes5, beatTimes6;
    [System.NonSerialized]
    public int[] beatSampleNos1, beatSampleNos2, beatSampleNos3, beatSampleNos4, beatSampleNos5, beatSampleNos6, beatSampleNosAll; //will use for update. Replacing beat times with beat sample numbers, for accuracy in converting.
    [System.NonSerialized]
    public int[] trueBeatTest; //this needs to be trimmed. working on it! //a list of sample numbers where there is a true beat (it's in order too iirc).
    [System.NonSerialized]
    public float[] energyMapArray; //the song's 'energy', at the time of each beat above.
    [System.NonSerialized]
    public int[] intervalArray; //the distance between each beat. Made for easier energy mapping.
    [System.NonSerialized]
    public float[] rollingAverages1, rollingAverages2, rollingAverages3, rollingAverages4, rollingAverages5, rollingAverages6;

    public float averageMagnitude; //aka, volume. average volume of (L and R) average.
    public float averageBeatMagnitude;
    public int averageIntervalSpan; //the average time between true beats.

    public int frequency; //Clip Freq. Taken directly. If I'm a god, shit WONT break if its not 44.1kHz, but plz don't try.
    public int samplesTotal; //Number of samples in a clip. Taken directly for ez access.
    public int averageBlockSize; //The size of each chunk to be averaged. Calc'd on Start and array updates. = freq / samples/sec (currently 441)
    public int reducedArraySize; //The length of the small array. Derived from the total no. of samples / the number of samples per block.
    public int magSamplesPerSecond = 100; //Full magnitude array's samples per second.

    private float[] frequencyBoundaries; //stores the freqs used in filtering. eg, 100hz, 200hz, 400hz...
    private int freqBands = 6;


    //CountBeats() variables/adjustables.
    [Range(0, 30)]
    public int deadZone = 8; //The number of samples before another beat may be recorded. 0.08s default.

    //getRollingAverage() variables/adjustables.
    [Range(5, 200)]
    public int windowWidth = 21; //the number of samples the rolling window average encompasses. 10 samples = 0.1s, since they are run on the reduced, 100s/s array. 21 default.
    [Range(0, 0.4f)]
    public float volumeNudge = 0.015f;
    [Range(0.5f, 5f)]
    public float RAmultiplier = 1.4f;

    //calculateTrueBeat() variables/adjustables.
    [Range(0, 5)]
    public int minBeatsMinusOne = 2; //minimum beats to record a true beat.
    [Range(0, 20)]
    public int trueBeatRange = 3; //check range for true beats (eg, all bands have a beat, but within a span of <4 samples).

    private SpectralFluxAnalyzer preProcessedSpectralFluxAnalyzer;


    // Use this for initialization
    void Start() { //be aware we shouldn't put too much in here alone; updating the clip must invoke these too.
        preProcessedSpectralFluxAnalyzer = new SpectralFluxAnalyzer();
        InitialiseFrequencyBounds();
        localAudioSource = GetComponent<AudioSource>();
        UpdateInternalVariables();//do this after initialising references, otherwise shit won't find audio clips, etc.
    }

    void OnValidate() { //runs every time the script? is validated. AKA, every time I update a variable in the editor.
        Debug.ClearDeveloperConsole(); //helps for reading!
        UpdateDataArrayPart2();
    }

    private void InitialiseFrequencyBounds() { //since we define top and bottom band, 7 values are needed for 6 ranges.
        frequencyBoundaries = new float[freqBands + 1];
        frequencyBoundaries[0] = 20f;
        frequencyBoundaries[1] = 60f;
        frequencyBoundaries[2] = 250f;
        frequencyBoundaries[3] = 500f;
        frequencyBoundaries[4] = 1500f;
        frequencyBoundaries[5] = 3000f;
        frequencyBoundaries[6] = 5000f; 
    }

    public void UpdateInternalVariables() {
        frequency = localAudioSource.clip.frequency; //BETTER BE 44.1KHZ
        samplesTotal = localAudioSource.clip.samples; //true variable. expect a number in the tens of millions. Small enough for regular int thank god. frequency x seconds of clip.
        averageBlockSize = frequency / magSamplesPerSecond;
        reducedArraySize = samplesTotal / averageBlockSize;
        if(LC != null) {
            LCL1 = new float[LC.Length]; //initialised LCl here, just in case. //USED TO BE CC
            LCL2 = new float[LC.Length]; 
            LCL3 = new float[LC.Length]; 
            LCL4 = new float[LC.Length]; 
            LCL5 = new float[LC.Length]; 
            LCL6 = new float[LC.Length]; 
        }
        else { Debug.Log("Warning; LC not initialised"); }
    }

    private float[] FilterArray(float[] subject, int bandNo) { //feed in relevant array. return filtered array.
        float[] array = new float[subject.Length];
        var lowF = BiQuadFilter.LowPassFilter(frequency, frequencyBoundaries[bandNo], 0.8f); // Keeps frequencies below x.
        var highF = BiQuadFilter.HighPassFilter(frequency, frequencyBoundaries[bandNo - 1], 1f); //note the minus. Keeps frequencies above x.
        for (int i = 0; i < array.Length; i++) {//low pass filterloop
            array[i] = lowF.Transform(subject[i]);
            array[i] = highF.Transform(array[i]); //not sure how to combine, trying, low then high filtering order.
        }
        return array;
    }

    private void TestFilterArray(float[] subject, int bandNo) {
        BiQuadFilter lowF = BiQuadFilter.LowPassFilter(frequency, frequencyBoundaries[bandNo], 0.8f);
        BiQuadFilter highF = BiQuadFilter.HighPassFilter(frequency, frequencyBoundaries[bandNo - 1], 1f);
        for(int i = 0; i < subject.Length; i++) {
            subject[i] = lowF.Transform(subject[i]);
            subject[i] = highF.Transform(subject[i]);
        }
    } //No longer needed!

    public void UpdateDataArray() { //Updates all data storage. Currently. //Called from FileMana... sets off whole update process. Splitting up slightly.
        Debug.Log("Updating data array at T=" + Time.time);
        UpdateInternalVariables();
        LCMagnitude = new float[samplesTotal];
        LCL1 = FilterArray(LC, 1);
        LCL2 = FilterArray(LC, 2);
        LCL3 = FilterArray(LC, 3);
        LCL4 = FilterArray(LC, 4);
        LCL5 = FilterArray(LC, 5);
        LCL6 = FilterArray(LC, 6);

        NormalizeThisArray(LCL1);// so today I've learned that passing an array to a function is actually just passing a pointer/reference.
        NormalizeThisArray(LCL2);// which seems really really weird to me cause passing variables just passes their content...
        NormalizeThisArray(LCL3);// so technically I could make that function not even return anything and it'll work. which feels silly.
        NormalizeThisArray(LCL4);
        NormalizeThisArray(LCL5);
        NormalizeThisArray(LCL6);

        LCMagnitude = NormalizeAnArray(LC); //updates magniLeft 
        RCMagnitude = NormalizeAnArray(RC); //updates magniRight
        //normalizing is standardised.

        //UpdateCombinedMagnitudeArray(); //updates overall, remember this must be done after prior.
        DownsampleArrays(); //create the arrays with reduced numbers of points. Currently 100 samples /s instead of 44.1k

        UpdateDataArrayPart2();
        if (gc != null) {
            gc.CreateGameMaps();
        }
    }

    public void UpdateDataArrayPart2() {
        //separated from the other part just for faster iteration and tweaking.
        rollingAverages1 = getRollingAverage(xLCL1, 1, windowWidth);
        rollingAverages2 = getRollingAverage(xLCL2, 2, windowWidth);
        rollingAverages3 = getRollingAverage(xLCL3, 3, -1); //this should be the same as the others.
        rollingAverages4 = getRollingAverage(xLCL4, 4, windowWidth);
        rollingAverages5 = getRollingAverage(xLCL5, 5, windowWidth);
        rollingAverages6 = getRollingAverage(xLCL6, 6, windowWidth);

        beatSampleNos1 = CountBeats(xLCL1, rollingAverages1, 1);
        beatSampleNos2 = CountBeats(xLCL2, rollingAverages2, 2);
        beatSampleNos3 = CountBeats(xLCL3, rollingAverages3, 3);
        beatSampleNos4 = CountBeats(xLCL4, rollingAverages4, 4);
        beatSampleNos5 = CountBeats(xLCL5, rollingAverages5, 5);
        beatSampleNos6 = CountBeats(xLCL6, rollingAverages6, 6);

        CalculateTrueBeat();
        CalculateBPMArray();
        CalculateAverageVolume();
        CalculateEnergyMap();
    }

    private void CalculateEnergyMap() { //creates an array parallel to the trueBeats array (aka, EMA[i] refers to trueBeat[i]). current song energy.
        //we'll derive it from a combination of;
        //x1 - the following intervals volume vs the average (whole song) volume.
        //x2 - the volume of the current beat vs the average beat volume.
        //x3 - the current bpm(interval) vs the interval average.

        energyMapArray = new float[trueBeatTest.Length];
        for (int i = 0; i < trueBeatTest.Length; i++) { //for each TrueBeat;
            float x1 = CompareIntervalVolume(i);
            float x2 = CompareBeatVolume(i);
            float x3 = CompareIntervalSpan(i) / 2;
            float xz = (x1 + x2 + x3) * 4; //multiplying to exaggerate difference, will reduce multiplier if sufficient songs have good range.
            xz = Mathf.Clamp(xz, -1f, 1f); //clamp to +- 1, may adjust with testing.
            xz = (float)Math.Round((double)xz, 2); //this seems retarded, but hey, if it works.
            energyMapArray[i] = xz;
            //Debug.Log("x1=" + x1 + " x2=" + x2 + " x3=" + x3 + " sum=" + energyMapArray[i]);
        }
        //((LCMagnitude[(j+1)*averageBlockSize] + RCMagnitude[(j+1)*averageBlockSize]) / 2) //
    }

    private float CompareIntervalSpan(int i) {
        //we're going to check the current bpm, so we're going to check the next 5 intervals and avg them.
        //so we'll return a 0 if we don't have 5 more entries to go through. -1 extra due to interval vs beats.
        if(intervalArray.Length - 6 <= i) {
            Debug.Log("reached end of array. exiting early.");
            return 0;
        }
        float sum = 0;
        float count = 0;
        for (int j = 0; j < 5; j++) {
            sum += intervalArray[i + j];
            count++;
        }
        float thisSpan = sum / count;
        //ok, so we have our upcoming interval(/bpm). now how do we convert it to something around +-1?
        //lets say max under beat is x3.
        //lets say max over beat is /3.
        //ok, this should output 1 at 1/3rd interval distance (3x faster). and -1 at the other end.
        float x = 0;
        if(thisSpan >= averageIntervalSpan) { //if the upcoming section is slower
            x = (averageIntervalSpan / thisSpan -1) / -2 ; //lets say usual = 60, this = 90
        }
        else { //lets say this = 60, usal span = 120
            x = (thisSpan / averageIntervalSpan - 1) / 2;
        }
        Mathf.Clamp(x, -1, 1);
        return x;
    }

    private float CompareBeatVolume(int i) {
        int intervalLength = trueBeatTest[i];
        //Debug.Log("i = " + i + " x = " + (xLCMagnitude[intervalLength] - averageBeatMagnitude));
        return xLCMagnitude[intervalLength] - averageBeatMagnitude;
    }

    private float CompareIntervalVolume(int i) { //this allows us to tell if we're in a quiet or loud section (relative to overall).
        //going to compare the next 5 interval volumes, so if we're near the end of the array, just return a neutral value.
        if(i >= trueBeatTest.Length - 5) { return 0f; }
        float sum = 0;
        for (int j = 0; j < 5; j++) {
            int intervalLength = intervalArray[i + j];
            int csn = trueBeatTest[i + j];//CurrentSampleNumber (for a reduced size array)
            sum += xLCMagnitude[csn + (intervalLength / 2) - 2]; //using 5 to help average a bit.
            sum += xLCMagnitude[csn + (intervalLength / 2) - 1]; 
            sum += xLCMagnitude[csn + (intervalLength / 2) - 0]; 
            sum += xLCMagnitude[csn + (intervalLength / 2) + 1];
            sum += xLCMagnitude[csn + (intervalLength / 2) + 2];
        }
        float average = sum / 25;
        //the next 5 beats is the roughly incoming section. all g. we're comparing this to the overall average volume of the song.
        float final = averageMagnitude - average; //volume of song overall vs calc.
        return final; //not magic numbering inside here JUST YET. gonna view stuff later.
    }


    private void CalculateBPMArray() {
        intervalArray = new int[trueBeatTest.Length - 1]; //since this only measures intervals, we subtract one from the length.
        for (int i = 0; i < trueBeatTest.Length -1; i++) {
            int x = trueBeatTest[i + 1] - trueBeatTest[i];
            intervalArray[i] = x;
        }
        float sum = 0;
        float count = 0;
        for (int i = 0; i < intervalArray.Length; i++) {
            sum += intervalArray[i];
            count++;
        }
        averageIntervalSpan = (int)(sum / count);
    }

    private void CalculateAverageVolume() {
        float averageLeft = CalculateAverage(LCMagnitude);
        float averageRight = CalculateAverage(RCMagnitude);
        averageMagnitude = (averageLeft + averageRight) / 2; //can add magic numbers later when in use.
        Debug.Log("avg magL = " + averageLeft + " and avg magR = " + averageRight); //should be numbers between 0.0 and 1.0. Probably around 0.4, depending on music.

        //calculate the average volume at each beat.
        float sum = 0;
        float count = 0;
        for(int i = 0; i < trueBeatTest.Length; i++) {
            int j = trueBeatTest[i]; //get the sample number from the truebeat array.
            sum += ((LCMagnitude[(j+1)*averageBlockSize] + RCMagnitude[(j+1)*averageBlockSize]) / 2); //check the TOTAL magnitude at that point. Will be > rolling average by at least a bit.
            count++;
        }
        averageBeatMagnitude = sum / count;
        Debug.Log("avg vol at beat = " + averageBeatMagnitude);
    }

    private float[] NormalizeAnArray(float[] copy) { //Modifies all negatives to positive.
        float[] localArray = copy;
        for (int i = 0; i < localArray.Length; i++) {
            localArray[i] = Math.Abs(copy[i]); //Conversion.
        }
        return localArray;
    } //Returns a new array, does not modify the one passed in.

    private void NormalizeThisArray(float[] copy) { //Directly affects the array you pass to it.
        for (int i = 0; i < copy.Length; i++) {
            copy[i] = Math.Abs(copy[i]);
        }
    } //Directly affects the array you pass to it.

    private int[] CountFlatLineGroupings(float[] data, int bandNo) { //this was our original test system. used a single threshold to check for greaterThans.
        float max = getMax(data);
        float min = getMin(data);
        int count = 0;
        float threshold = ((max - min) * 0.2f); //this may need fiddling. -J
        Debug.Log("Max = " + max + ". Min = " + min + ". Thresh = " + threshold + ".");
        
        for(int i = 0; i < data.Length - 6; i++) {
            if(data[i] > threshold && data[i + 1] < threshold && data[i+2] < threshold && data[i+3] < threshold && data[i+6] < threshold) { //personally, I think this line is retarded. I'll figure out something better later.
                count++; //instead of this, we can store the sample number in an array. but a count will do for now.
            }
        }
        int[] newArray = new int[count];
        int beatCount = 0;
        for (int i = 0; i < data.Length - 6; i++) {
            if (data[i] > threshold && data[i + 1] < threshold && data[i + 2] < threshold && data[i + 3] < threshold && data[i + 6] < threshold) { //personally, I think this line is retarded. I'll figure out something better later.
                newArray[beatCount] = i; //going through xLCL1, so stored samples have already been divided by 441 (converted to downsample).
                beatCount++;
            }
        }
        float bpm = beatCount / (localAudioSource.clip.length / 60);
        Debug.Log("Total beats for freqband " + bandNo + " = " + beatCount + ". Total BPM calculated as " + bpm + "bpm.");
        return newArray;
    } //no longer used; outdated :D

    private int[] CountBeats(float[] magnitudes, float[] averages, int bandNo) {
        int[] newArray = new int[magnitudes.Length];
        int beatCount = 0;
        //int deadZone = 8; //commented out, moved to top for more iterative tuning.
        //float volumeNudge = 0.015f; //commented out, moved to top for more iterative tuning.
        for (int i = deadZone; i < magnitudes.Length - deadZone; i++) {
            if (magnitudes[i] > averages[i]) { //nesting for speed. maybe. //IF this sample is greater than the rolling average, we'll check the area behind it...
                bool newBeat = true;
                for(int y = 1; y <= deadZone; y++) {
                    if(magnitudes[i-y] > (averages[i - y])) {
                        newBeat = false; //this isn't a beat; this sample is following another sample probably.
                        y = deadZone + 1; //escape the loop early.
                    }
                }
                if(newBeat == true) {
                    newArray[beatCount] = i;
                    beatCount++;
                }
            }
        }
        float bpm = beatCount / (localAudioSource.clip.length / 60);
        Debug.Log("Total beats for freqband " + bandNo + " = " + beatCount + ". Total BPM calculated as " + bpm + "bpm.");
        int index = Array.IndexOf(newArray, 0);
        int[] trimmedArray = new int[index];
        Array.Copy(newArray, 0, trimmedArray, 0, index);
        return trimmedArray;
    }

    private void CalculateTrueBeat() { //this code is filthy. I'm sorry. But it runs a lot faster than neat code.
        int totalSampleCount = beatSampleNos1.Length + beatSampleNos2.Length + beatSampleNos3.Length + beatSampleNos4.Length + beatSampleNos5.Length + beatSampleNos6.Length;
        beatSampleNosAll = new int[totalSampleCount];
        int bl1 = beatSampleNos1.Length;
        int bl2 = beatSampleNos2.Length;
        int bl3 = beatSampleNos3.Length;
        int bl4 = beatSampleNos4.Length;
        int bl5 = beatSampleNos5.Length;
        int bl6 = beatSampleNos6.Length; //copy pasted for faster writing
        Array.Copy(beatSampleNos1, 0, beatSampleNosAll, 0, bl1); //source > source starting index > destination > destination starting index > no. of elements to copy.
        Array.Copy(beatSampleNos2, 0, beatSampleNosAll, bl1, bl2); //these lines together move all 6 arrays into a combined array (cut to perfect length).
        Array.Copy(beatSampleNos3, 0, beatSampleNosAll, bl1+bl2, bl3);
        Array.Copy(beatSampleNos4, 0, beatSampleNosAll, bl1 + bl2 + bl3, bl4);
        Array.Copy(beatSampleNos5, 0, beatSampleNosAll, bl1 + bl2 + bl3 + bl4, bl5);
        Array.Copy(beatSampleNos6, 0, beatSampleNosAll, bl1 + bl2 + bl3 + bl4 + bl5, bl6);

        Array.Sort(beatSampleNosAll);//array sorted here. Woot!

        //now we'll try to make another array that, for now, simply counts up through the combined array, and adds times to another array when there are 3 beats within say 3 sample range.
        int[] tempArray = new int[totalSampleCount];
        int trueBeatCount = 0;
        for (int i = 0; i < beatSampleNosAll.Length; i++) {
            int currentSampleNo = beatSampleNosAll[i]; //current sample. eg, a beat at sample number 22.
            //int range = 3; //the distance we check for matching beats. //Moved to top (trueBeatRange) for iteration speed.
            int count = 0; //the number of matching beats we find.
            int arraySize = beatSampleNosAll.Length;
            for(int j = 1; j < 6; j++) { //since we have 6 bands possibly matching up, we check the next 5 array values.
                if(i+j < arraySize) { //this is some dirty nesting, but I couldn't think of a better AND faster way. rip. ensures we don't go out of bounds.
                    if (beatSampleNosAll[i + j] <= currentSampleNo + trueBeatRange) { //if the (target sample) is within 3 samples (time) of the target;
                        count++;
                    }
                }
                
            }
            if(count >= minBeatsMinusOne) { //if we have 3 beats within the same short span (the target sample + 2 matches) // >= was 2. Now moved to top.
                //add true beat to array.
                tempArray[trueBeatCount] = currentSampleNo;
                trueBeatCount++;
            }
            i += count; //WE MUST SKIP OVER MATCHES IF FOUND. This also includes if we find 2 beats (not the big 3) on the same spot. That's fine.
        }
        int index = Array.IndexOf(tempArray, 0); //returns the first instance of 0.
        int[] trimmedArray = new int[index]; //new array sized appropriately.
        Array.Copy(tempArray, 0, trimmedArray, 0, index); //copy everything up to that first 0 into the new temp array.
        //I should trim the tempArray, but lets see if this works first.
        trueBeatTest = trimmedArray;
    }

    private float getMax(float[] a) {
        float m = -Mathf.Infinity; //max.
        float n = a.Length;
        for(int i = 0; i < n; i++) {
            if(a[i] > m) {
                m = a[i];
            }
        }
        return m;
    } //Simple getMax func. Pass in an array, returns the highest float.

    private float getMin(float[] a) {
        float m = Mathf.Infinity; //min
        float n = a.Length;
        for(int i = 0; i < n; i++) {
            if(a[i] < m) {
                m = a[i];
            }
        }
        return m;
    } //Simple getMin func. Pass in an array, returns the lowest float.

    private void DownsampleArrays() {
        int newLength = LCMagnitude.Length / averageBlockSize; //currently 100 per second.
        xCCMagnitude = new float[reducedArraySize]; //same length since will be combined LR data. add +1 to length if paranoid.

        Debug.Log("Running new code...");
        xLCMagnitude = DownsampleArray(LCMagnitude);
        xRCMagnitude = DownsampleArray(RCMagnitude);
        xrLC = getRollingAverage(xRCMagnitude, 1, 40); // -J add rolling average line thingos here. we may want to force specify a width, or make a new function.
        xrRC = getRollingAverage(xLCMagnitude, 1, 40); // -J add rolling average line thingos here. we may want to force specify a width, or make a new function.

        xLCL1 = DownsampleArray(LCL1);
        xLCL2 = DownsampleArray(LCL2);
        xLCL3 = DownsampleArray(LCL3);
        xLCL4 = DownsampleArray(LCL4);
        xLCL5 = DownsampleArray(LCL5);
        xLCL6 = DownsampleArray(LCL6);
        Debug.Log("No errors.");

        //quick subtraction for the combined channel array;
        for(int i = 0; i < xLCMagnitude.Length; i++) {
            xCCMagnitude[i] = xLCMagnitude[i] - xRCMagnitude[i];
        }

        Debug.Log("Reduced array created. Array length of xLC = " + xLCMagnitude.Length);
        //Debug.Log("hey. " + xLCMagnitude[1] + xLCMagnitude[101] + xLCMagnitude[202] + xLCMagnitude[303] + xLCMagnitude[404]);
    } //master function to reduce the size of all the arrays. I might just make a small function to do it per array..

    private float[] DownsampleArray(float[] inputArray) { 
        float[] newArray = new float[reducedArraySize];
        int count = 0;
        float sum = 0;
        int z = 0; //counter for the reduced array entries. //keeps track of which part of the new array we're up to.
        for(int i = 0; i < inputArray.Length; i++) { 
            sum += inputArray[i];
            count++;
            if(count >= averageBlockSize || i == inputArray.Length) { //if we've added one block worth OR reached the end of the array;
                newArray[z] = sum / count;
                count = 0;
                sum = 0;
                z++; //This runs a lot. For each array in a 5 minute song; 30,000 times. God bless GHz CPUs.
            }
        }
        return newArray;
    } //Downsamples array. Reduces every (441) samples into one, averaged value in the returned array. Last uneven block should be dealt with just fine.

    public int getIndexFromTime(float curTime) {
        float lengthPerSample = localAudioSource.clip.length / (float)samplesTotal;

        return Mathf.FloorToInt(curTime / lengthPerSample);
    }

    public float getTimeFromIndex(int index) {
        return ((1f / frequency) * index);
    }

    public void getFullSpectrumThreaded() { //not used. from https://medium.com/giant-scam/algorithmic-beat-mapping-in-unity-preprocessed-audio-analysis-d41c339c135a . //I find it well visualised, but overly sensitive (doesnt seem to target lower freq, which it should).
        try {
            // We only need to retain the samples for combined channels over the time domain
            float[] preProcessedSamples = new float[this.samplesTotal]; //J- check here to see if needs doubling/halving.

            int numProcessed = 0;
            float combinedChannelAverage = 0f;
            for (int i = 0; i < CC.Length; i++) {
                combinedChannelAverage += CC[i];

                // Each time we have processed all channels samples for a point in time, we will store the average of the channels combined
                if ((i + 1) % 2 == 0) { //hard coding here, soz. J-
                    preProcessedSamples[numProcessed] = combinedChannelAverage / 2; //here too (the 2).
                    numProcessed++;
                    combinedChannelAverage = 0f;
                }
            }

            Debug.Log("Combine Channels done");
            Debug.Log(preProcessedSamples.Length);

            // Once we have our audio sample data prepared, we can execute an FFT to return the spectrum data over the time domain
            int spectrumSampleSize = 1024;
            int iterations = preProcessedSamples.Length / spectrumSampleSize;

            FFT fft = new FFT();
            fft.Initialize((UInt32)spectrumSampleSize);

            Debug.Log(string.Format("Processing {0} time domain samples for FFT", iterations));
            double[] sampleChunk = new double[spectrumSampleSize];
            for (int i = 0; i < iterations; i++) {
                // Grab the current 1024 chunk of audio sample data
                Array.Copy(preProcessedSamples, i * spectrumSampleSize, sampleChunk, 0, spectrumSampleSize);

                // Apply our chosen FFT Window
                double[] windowCoefs = DSP.Window.Coefficients(DSP.Window.Type.Hanning, (uint)spectrumSampleSize);
                double[] scaledSpectrumChunk = DSP.Math.Multiply(sampleChunk, windowCoefs);
                double scaleFactor = DSP.Window.ScaleFactor.Signal(windowCoefs);

                // Perform the FFT and convert output (complex numbers) to Magnitude
                System.Numerics.Complex[] fftSpectrum = fft.Execute(scaledSpectrumChunk);
                double[] scaledFFTSpectrum = DSPLib.DSP.ConvertComplex.ToMagnitude(fftSpectrum);
                scaledFFTSpectrum = DSP.Math.Multiply(scaledFFTSpectrum, scaleFactor);

                // These 1024 magnitude values correspond (roughly) to a single point in the audio timeline
                float curSongTime = getTimeFromIndex(i) * spectrumSampleSize;

                // Send our magnitude data off to our Spectral Flux Analyzer to be analyzed for peaks
                preProcessedSpectralFluxAnalyzer.analyzeSpectrum(Array.ConvertAll(scaledFFTSpectrum, x => (float)x), curSongTime);
            }

            Debug.Log("Spectrum Analysis done");
            Debug.Log("Background Thread Completed");

        }
        catch (Exception e) {
            // Catch exceptions here since the background thread won't always surface the exception to the main thread
            Debug.Log(e.ToString());
        }
    }//Not used currently. Copied from giant-scam. Using for reference sporadically.

    private float CalculateAverage(float[] inputArray) { //for float arrays.
        float sum = 0;
        float count = 0; //could be int I'm pretty sure but DONT TRUST MIXING IT.
        foreach (float x in inputArray) {
            sum += x;
            count++;
        }
        return sum / count;
    }

    private float[] getRollingAverage(float[] inputArray, int bandNo, int w) { //ww is the window width. if in doubt, leave it to the global, since that can be updated live.
        if (inputArray == null) { return null; } //only occurs during load.
        int arrayLength = inputArray.Length;
        float[] outputArray = new float[arrayLength]; //maximum possible size. trimming later.
        float sum = 0; //the sum. Added to until reaching the window width, then added AND subtracted from.
        int count = 0; //number of values contributing to the count.
        int leftSample = 0; //the sampleNo for the leftmost sample in the window (the trailing number).
        int rightSample = 0; //the sampleNo for the rightmost sample in the window (the leading number).
        int ww;
        if (w <= -1) { ww = windowWidth; } else { ww = w; } //if passed in width is invalid, use the global. otherwise, use the one passed in. This way, you can pass in a hard-coded width, or use an invalid/the global directly.

        for(int i = 0; i < arrayLength + ww - 2; i++) { //The -2 is because I suck. It would be -1 if I was smarter.
            if(rightSample < arrayLength) {
                sum = sum + inputArray[rightSample];
                count++;
                rightSample++;
            }
            if(ww <= rightSample - leftSample || rightSample == arrayLength) {
                sum = sum - inputArray[leftSample]; //we remove this value from the sum.
                count--; //we removed one sample from the window.
                leftSample++; //we advance the recorded position of the leftmost sample.
            }
            float average = sum / count; //easy average calc.
            average = Mathf.Clamp(average, 0f, 1f); //clamp values to prevent shit breaking later on.
            int savespot = (rightSample + leftSample) / 2; //we must save to the middle of our window after all!
            average = average * RAmultiplier + volumeNudge; //space for multipliers + flats. currently 1.4x and +0.015.
            outputArray[savespot] = average;
            /*if(i == 5 || i == 6 || i == 7 || i == 8 || i == 9 || i == 10) {
                Debug.Log("i = " + i + "lftSmp = " + inputArray[leftSample] + " rhtSmp = " + inputArray[rightSample] + " iSmp = " + inputArray[savespot]);
                Debug.Log("avg = " + average + " sum = " + sum + " count = " + count);
            }*/
        }
        return outputArray;//Tada!
    }
}

public class WaveMemoryStream : System.IO.MemoryStream { //had to include the system.io at first, now it's fine. //not used in the end.
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return false; } }
    public override bool CanRead { get { return true; } }
    public override long Length { get { return _waveStream.Length; } }
    public override long Position { get { return _waveStream.Position; } set { _waveStream.Position = value; } }

    private MemoryStream _waveStream;

    public WaveMemoryStream(byte[] sampleData, int audioSampleRate, ushort audioBitsPerSample, ushort audioChannels) {
        _waveStream = new MemoryStream();
        WriteHeader(_waveStream, sampleData.Length, audioSampleRate, audioBitsPerSample, audioChannels);
        WriteSamples(_waveStream, sampleData);
        _waveStream.Position = 0;
    }

    public void WriteHeader(Stream stream, int length, int audioSampleRate, ushort audioBitsPerSample, ushort audioChannels) {
        BinaryWriter bw = new BinaryWriter(stream);

        bw.Write(new char[4] { 'R', 'I', 'F', 'F' });
        int fileSize = 36 + length;
        bw.Write(fileSize);
        bw.Write(new char[8] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        bw.Write((int)16);
        bw.Write((short)1);
        bw.Write((short)audioChannels);
        bw.Write(audioSampleRate);
        bw.Write((int)(audioSampleRate * ((audioBitsPerSample * audioChannels) / 8)));
        bw.Write((short)((audioBitsPerSample * audioChannels) / 8));
        bw.Write((short)audioBitsPerSample);

        bw.Write(new char[4] { 'd', 'a', 't', 'a' });
        bw.Write(length);
    }

    public void WriteSamples(Stream stream, byte[] sampleData) {
        BinaryWriter bw = new BinaryWriter(stream);
        bw.Write(sampleData, 0, sampleData.Length);
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return _waveStream.Read(buffer, offset, count);
    }

    public virtual void WriteTo(Stream stream) {
        int bytesRead = 0;
        byte[] buffer = new byte[8192];

        do {
            bytesRead = Read(buffer, 0, buffer.Length);
            stream.Write(buffer, 0, bytesRead);
        } while (bytesRead > 0);

        stream.Flush();
    }

    public override void Flush() {
        _waveStream.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin) {
        return _waveStream.Seek(offset, origin);
    }

    public override void SetLength(long value) {
        throw new NotImplementedException();
    }
    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotImplementedException();
    }
} //Also not used I think. Leaving until I can confirm I don't need it.
