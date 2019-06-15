import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-fetch-vudu',
  templateUrl: './fetch-vudu.component.html'
})
export class FetchVuduComponent {
  public results: VuduResult[];

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    http.get<VuduResult[]>(baseUrl + 'api/SampleData/VuduResults').subscribe(result => {
      this.results = result;
    }, error => console.error(error));
  }
}

interface VuduResult {
  title: string;
  contentId: string;
  superType: string;
  type: string;
  posterUrl: string;    
  description: string;  
  dynamicRange: string;
  releaseDate: Date;
}
