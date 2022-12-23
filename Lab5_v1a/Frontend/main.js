document.getElementById("load-images").onclick = LoadImages
document.getElementById("compare-images").onclick = CompareImages
document.getElementById("clear-images").onclick = ClearImages
document.getElementById("update-storage-info").onclick = UpdateStorageInfo
document.getElementById("clear-storage").onclick = ClearStorage

const server_addr = 'http://localhost:5000'
let active_images = []
comparison_running = false

function readAsDataURL(file) {
    return new Promise((res, rej) => {
        const reader = new FileReader()
        reader.onloadend = e => res(e.target.result)
        reader.onerror = e => rej(e)
        reader.readAsDataURL(file)
    })
}

function CreateComparisonCell(dst, sim) {
    let ret = document.createElement('p')
    let p = document.createElement('p')
    p.setAttribute('class', 'distance-value')
    p.innerHTML = `${dst}`
    ret.appendChild(p)
    p = document.createElement('p')
    p.setAttribute('class', 'similarity-value')
    p.innerHTML = `  ${sim}`
    ret.appendChild(p)
    return ret
}

async function LoadImages(){
    await ClearImages()
    let files = document.getElementById('files-input').files
    let tbdy = document.getElementById('comparisons-table-body');
    let tr = document.createElement('tr')
    tr.appendChild(CreateComparisonCell('distance', 'similarity'))
    for (let f of files) {
        let img = document.createElement('img')
        let dataurl = await readAsDataURL(f)
        let data = await dataurl.split(',')[1]
        await img.setAttribute('src', `data:image/jpg;base64,${data}`)
        if (img.height != 112 || img.width != 112) {
            throw new Error(`Invalid image size: ${img.height} ${img.width}`)
        }
        let td = document.createElement('td')
        td.appendChild(img)
        tr.appendChild(td)

        let image = {}
        image.name = f.name
        let details = {}
        details.data = data
        image.details = details

        const req = {
            mode: 'cors',
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(image)
        }
        let response = await fetch(server_addr + '/images', req)
        image.id = await response.json()
        active_images.push(image)
    }
    tbdy.appendChild(tr)
    UpdateStorageInfo()
}

function distance(x1, x2) {
    let dst = 0.0
    let n = x1.length
    for (var i = 0; i < n; i++) {
        dst = dst + Math.pow((x1[i] - x2[i]), 2)
    }
    return Math.sqrt(dst)
}

function similarity(x1, x2) {
    let sim = 0.0
    let n = x1.length
    for (var i = 0; i < n; i++) {
        sim += x1[i] * x2[i]
    }
    return sim
}

async function CompareImages(){
    if (comparison_running) {
        return
    }
    try{
        comparison_running = true
        let tbdy = document.getElementById('comparisons-table-body');

        for (let image of active_images) {
            response = await fetch(server_addr + '/images/' + image.id)
            image_info = await response.json()
            image.embedding = decodeEmbedding(image_info.embedding)
        }

        for (let image of active_images) {
            let tr = document.createElement('tr')
            let img = document.createElement('img')
            img.setAttribute('src', `data:image/png;base64,${image.details.data}`)
            let td = document.createElement('td')
            td.appendChild(img)
            tr.appendChild(td)
            for (let image_2 of active_images) {
                dst = distance(image.embedding, image_2.embedding)
                sim = similarity(image.embedding, image_2.embedding)
                p = CreateComparisonCell(dst.toFixed(3), sim.toFixed(3))
                let td = document.createElement('td')
                td.appendChild(p)
                tr.appendChild(td)
            }

            tbdy.appendChild(tr)
        }
    }
    catch(e){
        console.log(e)
    }
    comparison_running = false
}

async function ClearImages(){
    if (comparison_running) {
        return
    }
    active_images = []
    document.getElementById('comparisons-table-body').replaceChildren()
}

function decodeEmbedding(encodedString) {
    decodedString = atob(encodedString)
    var data = []
    for (var i = 0; i < decodedString.length; ++i) {
        var code = decodedString.charCodeAt(i)
        data = data.concat([code])
    }
    var buf = new ArrayBuffer(data.length)
    var view = new DataView(buf)
    for (var i = 0; i < data.length; i++) {
        view.setUint8(i, data[i])
    }
    embedding = []
    for (var i = 0; i < 512; i++) {
        embedding.push(view.getFloat32(i * 4, true))
    }
    return embedding
}

async function UpdateStorageInfo(){
    let storageInfoElem = document.getElementById('storage-data-items')
    storageInfoElem.replaceChildren()
    let response = await fetch(server_addr + '/images')
    let remote_images = await response.json()
    for (let image of remote_images) {
        let p = document.createElement('p')
        p.innerHTML = image.name
        let img = document.createElement('img')
        let data = image.details.data
        img.setAttribute('src', `data:image/png;base64,${data}`)
        let div = document.createElement('div')
        div.appendChild(img)
        div.appendChild(p)
        div.setAttribute('class', 'storage-item')
        storageInfoElem.appendChild(div)
    }
}
UpdateStorageInfo()

async function ClearStorage(){
    response = await fetch(server_addr + '/images', {
        method: 'DELETE',
        headers: {
            'Content-type': 'application/json'
        }
    })
    UpdateStorageInfo()
}
