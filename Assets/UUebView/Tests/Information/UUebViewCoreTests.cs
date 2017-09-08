
using System;
using Miyamasu;
using UnityEngine;
using UUebView;

/**
    test for UUebView generator.
*/
public class UUebViewCoreTests : MiyamasuTestRunner {
    GameObject eventReceiverGameObj;
    GameObject view;
    
    private int index;
    private void Show (GameObject view, Action loaded=null) {
        RunOnMainThread(
            () => {
                var canvas = GameObject.Find("Canvas/UUebViewCoreTestPlace");
                var baseObj = new GameObject("base");
                

                // ベースオブジェクトを見やすい位置に移動
                var baseObjRect = baseObj.AddComponent<RectTransform>();
                baseObjRect.anchoredPosition = new Vector2(100 * index, 0);
                baseObjRect.anchorMin = new Vector2(0,1);
                baseObjRect.anchorMax = new Vector2(0,1);
                baseObjRect.pivot = new Vector2(0,1);
                

                baseObj.transform.SetParent(canvas.transform, false);

                view.transform.SetParent(baseObj.transform, false);

                index++;
                if (loaded != null) {
                    loaded();
                }
            }
        );
    }

    [MSetup] public void Setup () {
        RunOnMainThread(
            () => {
                eventReceiverGameObj = new GameObject("controller");
                eventReceiverGameObj.AddComponent<TestReceiver>();
            }
        );
    }

    private void ShowLayoutRecursive (TagTree tree, ResourceLoader loader) {
        Debug.Log("tree:" + loader.GetTagFromValue(tree.tagValue) + " offsetX:" + tree.offsetX + " offsetY:" + tree.offsetY + " width:" + tree.viewWidth + " height:" + tree.viewHeight);
        foreach (var child in tree.GetChildren()) {
            ShowLayoutRecursive(child, loader);
        }
    }

    [MTest] public void GenerateSingleViewFromSource () {
        var source = @"
<body>something1.<img src='https://dummyimage.com/100.png/09f/fff'/></body>";
        
        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void GenerateSingleViewFromUrl () {
        var url = "resources://UUebViewTest/UUebViewTest.html";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromUrl(eventReceiverGameObj, url, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void LoadThenReload () {
        var source = @"
<body>
    reload sample.
    <img src='https://dummyimage.com/100.png/09f/fff'/>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );

        var done2 = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done2 = true;
                };
                var core = view.GetComponent<UUebViewComponent>().Core;
                core.Reload();
            }
        );

        WaitUntil(
            () => done2, 5, "too late."
        );
    }

    [MTest] public void ShowAndHide () {
        var source = @"
<body>
    something3.
    <img src='https://dummyimage.com/100.png/09f/fff' id='button' button='true'/>
    <p hidden='false' listen='button'>else other long text.<a href='somewhere'> and link.</a></p>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void HideAndShow () {
        var source = @"
<body>
    something3.
    <img src='https://dummyimage.com/100.png/09f/fff' id='button' button='true'/>
    <p hidden='true' listen='button'>else</p>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void CascadeButton () {
        var source = @"
<body>
    <img src='https://dummyimage.com/100.png/09f/fff' id='button' button='true'/>
    <img hidden='true' src='https://dummyimage.com/100.png/08f/fff' id='button2' button='true' listen='button'/>
    <img hidden='true' src='https://dummyimage.com/100.png/07f/fff' id='button3' button='true' listen='button2'/>
    <img hidden='true' src='https://dummyimage.com/100.png/06f/fff' id='button4' button='true' listen='button3'/>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void CachedContent () {
        var source = @"
<body>
    <img src='https://dummyimage.com/100.png/09f/fff' id='button' button='true'/>
    <img hidden='true' src='https://dummyimage.com/100.png/08f/fff' id='button2' button='true' listen='button'/>
    <img hidden='true' src='https://dummyimage.com/100.png/07f/fff' id='button3' button='true' listen='button2'/>
    <img hidden='true' src='https://dummyimage.com/100.png/06f/fff' id='button4' button='true' listen='button3'/>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void ShowLinkByButton () {
        var source = @"
<body>
    something3.
    <img src='https://dummyimage.com/100.png/09f/fff' id='button1' button='true'/>
    <a href='href test' hidden='true' listen='button1'>link</a>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void ManyImages () {
        var source = @"
<!DOCTYPE uuebview href='resources://Views/LayoutHTMLWithCustomTag/UUebTags'>
<body>
    something4.
    <customimg src='https://dummyimage.com/100.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/101.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/102.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/103.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/104.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/105.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/106.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/107.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/108.png/09f/fff' id='button1' button='true'/>
    <customimg src='https://dummyimage.com/109.png/09f/fff' id='button1' button='true'/>
    <a href='href test' hidden='true' listen='button1'>link</a>
</body>";

        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(100,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void Sample2 () {
        var source = @"
<!DOCTYPE uuebview href='resources://Views/MyInfoView/UUebTags'>
<body>
    <bg>
    	<titlebox>
    		<titletext>レモン一個ぶんのビタミンC</titletext>
    	</titlebox>
    	<newbadge></newbadge>
    	<textbg>
    		<textbox>
	    		<updatetext>koko ni nihongo ga iikanji ni hairu. good thing. long text will make large window. like this.</updatetext>
	    		<updatetext hidden='true' listen='readmore'>omake!<img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/><img src='https://dummyimage.com/100.png/07f/fff'/></updatetext>
                <img src='https://dummyimage.com/100.png/09f/fff' button='true' id='readmore'/>
	    	</textbox>
	    </textbg>
    </bg>
</body>";
        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(300,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }
    
    [MTest] public void HideThenShow () {
        var source = @"
<!DOCTYPE uuebview href='resources://Views/MyInfoView/UUebTags'>
<bg>
    <textbg>
        <textbox>
            <updatetext>koko ni nihongo ga iikanji ni hairu.<br> good thing. long text will make large window. like this.</updatetext>
            <updatetext hidden='true' listen='readmore'>omake!</updatetext>
        </textbox>
    </textbg>
</bg>";
        UUebView.UUebViewComponent uUebView = null;

        var done = false;
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    uUebView = view.GetComponent<UUebViewComponent>();
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(300,100));
            }
        );

        var shown = false;
        Show(view, () => {shown = true;});

        WaitUntil(
            () => shown && done, 5, "too late."
        );
        
        // show hidden contents.
        {
            var updated = false;
            RunOnMainThread(
                () => {
                    eventReceiverGameObj.GetComponent<TestReceiver>().OnUpdated = () => {
                        updated = true;
                    };
                    uUebView.EmitButtonEventById(null, string.Empty, "readmore");
                }
            );

            WaitUntil(
                () => updated, 5, "too late."
            );
        }
        
        // hide hidden contents again.
        {
            var updated = false;
            RunOnMainThread(
                () => {
                    eventReceiverGameObj.GetComponent<TestReceiver>().OnUpdated = () => {
                        updated = true;
                    };
                    uUebView.EmitButtonEventById(null, string.Empty, "readmore");
                }
            );

            WaitUntil(
                () => updated, 5, "too late."
            );
        }

        var tree = uUebView.Core.layoutedTree;
        var targetTextBox = tree.GetChildren()[0].GetChildren()[0].GetChildren()[0].GetChildren()[0].GetChildren()[0].GetChildren()[0].GetChildren()[1];
        Assert(targetTextBox.offsetY == 26f, "not match, targetTextBox.offsetY:" + targetTextBox.offsetY);
        // ShowLayoutRecursive(tree, uUebView.Core.resLoader);
    }

    [MTest] public void UnityRichTextColorSupport () {
        var source = @"
<body>
    <color=#ff0000ff>red</color> <color=#008000ff>green</color> <color=#0000ffff>blue</color>
</body>";
        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(300,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void UnityRichTextSizeSupport () {
        var source = @"
<p>
    a<size=50>large string</size>b
</p>";
        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(300,100));
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }

    [MTest] public void SetViewName () {
        var viewName = "SetViewName";
        var source = @"
<body>
    <color=#ff0000ff>red</color> <color=#008000ff>green</color> <color=#0000ffff>blue</color>
</body>";
        var done = false;
        
        RunOnMainThread(
            () => {
                eventReceiverGameObj.GetComponent<TestReceiver>().OnLoaded = () => {
                    done = true;
                };
                view = UUebView.UUebViewComponent.GenerateSingleViewFromHTML(eventReceiverGameObj, source, new Vector2(300,100), null,null, viewName);
            }
        );
        
        Show(view);

        WaitUntil(
            () => done, 5, "too late."
        );
    }
}